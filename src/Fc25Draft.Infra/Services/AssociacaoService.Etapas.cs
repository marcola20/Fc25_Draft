using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Resultado e etapas seguintes da eleição (Art. 10 do edital): maioria absoluta, 2º turno, votação de desempate
/// (uma só) e, se empatar de novo, decisão da Presidência da CBFV.
/// </summary>
public partial class AssociacaoService
{
    public async Task<string> PublicarResultadoAsync(Guid eleicaoId, CancellationToken ct)
    {
        var eleicao = await BuscarAsync(eleicaoId, ct);
        if (eleicao.Situacao != SituacaoEleicao.Apuracao) throw new InvalidOperationException("Encerre a votação antes de publicar.");

        // Art. 10, III: vence quem passar de 50% dos votos válidos (brancos não contam).
        eleicao.ChapaVencedoraId = VencedoraPelaContagem(eleicao, await VotosAsync(eleicaoId, ct));
        eleicao.Situacao = SituacaoEleicao.Resultado;
        eleicao.ResultadoEm = Agora;
        await _db.SaveChangesAsync(ct);

        var desfecho = await DesfechoAsync(eleicao, eleicao.ChapaVencedoraId, ct);
        return desfecho.Tipo is TipoDesfecho.SegundoTurno or TipoDesfecho.Desempate
            ? $"Resultado publicado. {desfecho.Texto} Crie a próxima etapa."
            : $"Resultado publicado. {desfecho.Texto}";
    }

    public async Task<EleicaoDto> CriarProximaEtapaAsync(Guid eleicaoId, CancellationToken ct)
    {
        var eleicao = await BuscarAsync(eleicaoId, ct);
        if (eleicao.Situacao != SituacaoEleicao.Resultado) throw new InvalidOperationException("Publique o resultado antes.");

        var desfecho = await DesfechoAsync(eleicao, eleicao.ChapaVencedoraId, ct);
        return desfecho.Tipo switch
        {
            TipoDesfecho.SegundoTurno => await CriarEtapaAsync(eleicao, desfecho.Garantidas, turno: 2, desempate: false, ct),
            TipoDesfecho.Desempate => await CriarEtapaAsync(eleicao, desfecho.Empatadas, eleicao.Turno, desempate: true, ct),
            TipoDesfecho.DecisaoPresidencia => throw new InvalidOperationException("O desempate empatou de novo: agora quem decide é a Presidência (Art. 10, VI e VII)."),
            _ => throw new InvalidOperationException("Este resultado não pede outra etapa.")
        };
    }

    public async Task<string> DecidirPelaPresidenciaAsync(Guid eleicaoId, IReadOnlyCollection<Guid> chapas, CancellationToken ct)
    {
        var eleicao = await BuscarAsync(eleicaoId, ct);
        if (eleicao.Situacao != SituacaoEleicao.Resultado) throw new InvalidOperationException("Publique o resultado antes.");

        var desfecho = await DesfechoAsync(eleicao, eleicao.ChapaVencedoraId, ct);
        if (desfecho.Tipo != TipoDesfecho.DecisaoPresidencia)
            throw new InvalidOperationException("Só cabe decisão da Presidência quando o desempate empata de novo.");

        var escolhidas = chapas.Distinct().ToList();
        if (escolhidas.Count != desfecho.Vagas || escolhidas.Any(c => !desfecho.Empatadas.Contains(c)))
            throw new InvalidOperationException($"Escolha {desfecho.Vagas} chapa(s) entre as empatadas.");

        var nomes = await NomesAsync(escolhidas, ct);
        var texto = string.Join(" e ", escolhidas.Select(id => nomes[id]));

        if (eleicao.Turno >= 2)
        {
            eleicao.ChapaVencedoraId = escolhidas[0];
            eleicao.DecisaoPresidencia = $"O desempate empatou de novo e a Presidência da CBFV escolheu a chapa {texto} (Art. 10, VII).";
            await _db.SaveChangesAsync(ct);
            return $"Chapa {texto} eleita por decisão da Presidência.";
        }

        eleicao.DecisaoPresidencia = $"O desempate empatou de novo e a Presidência da CBFV levou {texto} ao 2º turno (Art. 10, VI).";
        await _db.SaveChangesAsync(ct);
        await CriarEtapaAsync(eleicao, desfecho.Garantidas.Concat(escolhidas).ToList(), turno: 2, desempate: false, ct);
        return $"{texto} vai ao 2º turno por decisão da Presidência. O 2º turno foi criado em rascunho.";
    }

    /// <summary>
    /// O que a contagem decide. Com <paramref name="vencedora"/> há chapa eleita. Senão: o 1º turno manda duas ao
    /// 2º (empate na fronteira = desempate); o desempate do 1º preenche as vagas que o 1º deixou; o 2º turno e o
    /// desempate dele elegem uma. Desempate que empata de novo vai para a Presidência.
    /// </summary>
    private async Task<DesfechoEleicaoDto> DesfechoAsync(EleicaoAssociacao e, Guid? vencedora, CancellationToken ct)
    {
        var chapas = await _db.AssociacaoChapas.AsNoTracking().Where(c => c.EleicaoId == e.EleicaoId).ToListAsync(ct);
        var votos = await VotosAsync(e.EleicaoId, ct);
        int V(ChapaAssociacao c) => votos.GetValueOrDefault(c.ChapaId);
        static string Nomes(IEnumerable<ChapaAssociacao> cs) => string.Join(", ", cs.Select(c => c.Nome));
        var vazio = Array.Empty<Guid>();
        var validos = votos.Values.Sum();

        if (vencedora is Guid v && chapas.FirstOrDefault(c => c.ChapaId == v) is { } eleita)
            return new(TipoDesfecho.Eleita,
                e.DecisaoPresidencia ?? $"Chapa {eleita.Nome} eleita com {V(eleita)} de {validos} votos válidos.",
                new[] { v }, vazio, 0);

        if (e.Turno == 1 && e.Desempate && e.DecisaoPresidencia is not null)
            return new(TipoDesfecho.Decidida, e.DecisaoPresidencia, vazio, vazio, 0);

        if (validos == 0 && e.Turno == 1 && !e.Desempate)
            return new(TipoDesfecho.SemVotos, "Nenhum voto válido: nenhuma chapa eleita.", vazio, vazio, 0);

        // Quantas seguem, e quem já seguia antes desta etapa (no desempate do 1º turno, as classificadas direto).
        var vagas = e.Turno >= 2 ? 1 : 2;
        IReadOnlyList<Guid> jaSeguiam = vazio;
        if (e.Turno == 1 && e.Desempate)
        {
            var anterior = e.AnteriorId is Guid a
                ? await _db.AssociacaoEleicoes.AsNoTracking().FirstOrDefaultAsync(x => x.EleicaoId == a, ct)
                : null;
            if (anterior is null) throw new InvalidOperationException("Não achei o 1º turno deste desempate.");
            var doPrimeiro = await DesfechoAsync(anterior, null, ct);
            vagas = doPrimeiro.Vagas;
            jaSeguiam = doPrimeiro.Garantidas;
        }

        var ordem = chapas.OrderByDescending(V).ThenBy(c => c.Numero).ToList();
        List<ChapaAssociacao> acima, empatadas;
        if (ordem.Count <= vagas || V(ordem[vagas - 1]) > V(ordem[vagas]))
        {
            acima = ordem.Take(vagas).ToList();
            empatadas = new();
        }
        else
        {
            var limite = V(ordem[vagas - 1]);
            acima = ordem.Where(c => V(c) > limite).ToList();
            empatadas = ordem.Where(c => V(c) == limite).ToList();
        }
        var garantidas = jaSeguiam.Concat(acima.Select(c => c.ChapaId)).ToList();
        var vagasNoEmpate = vagas - acima.Count;

        if (empatadas.Count == 0)
        {
            // Sem empate na fronteira e sem eleita: é o 1º turno (ou o desempate dele) mandando ao 2º turno.
            var nomes = await NomesAsync(garantidas, ct);
            var prefixo = e.Desempate
                ? $"Desempate: {Nomes(acima)} vai ao 2º turno. "
                : "Nenhuma chapa passou de 50% dos votos válidos. ";
            return new(TipoDesfecho.SegundoTurno,
                prefixo + $"2º turno entre {string.Join(" e ", garantidas.Select(id => nomes[id]))}.",
                garantidas, vazio, 0);
        }

        var ids = empatadas.Select(c => c.ChapaId).ToList();
        if (e.Desempate)
            return new(TipoDesfecho.DecisaoPresidencia,
                e.Turno >= 2
                    ? $"O desempate empatou de novo entre {Nomes(empatadas)}: a Presidência da CBFV decide a chapa eleita (Art. 10, VII)."
                    : $"O desempate empatou de novo entre {Nomes(empatadas)}: a Presidência da CBFV decide quem vai ao 2º turno (Art. 10, VI).",
                garantidas, ids, vagasNoEmpate);

        return new(TipoDesfecho.Desempate,
            e.Turno >= 2
                ? $"Empate no 2º turno entre {Nomes(empatadas)}: votação de desempate em até 48 horas (Art. 10, VII)."
                : acima.Count > 0
                    ? $"{Nomes(acima)} vai ao 2º turno. Empate pela outra vaga entre {Nomes(empatadas)}: votação de desempate em até 48 horas (Art. 10, V)."
                    : $"Empate entre {Nomes(empatadas)} pelas vagas do 2º turno: votação de desempate em até 48 horas (Art. 10, V).",
            garantidas, ids, vagasNoEmpate);
    }

    /// <summary>Nova etapa em rascunho com estas chapas (copiadas, com os mesmos membros) e os mesmos eleitores.</summary>
    private async Task<EleicaoDto> CriarEtapaAsync(EleicaoAssociacao origem, IReadOnlyList<Guid> chapaIds, int turno, bool desempate, CancellationToken ct)
    {
        if (await _db.AssociacaoEleicoes.AnyAsync(x => x.AnteriorId == origem.EleicaoId, ct))
            throw new InvalidOperationException("A próxima etapa desta eleição já foi criada.");

        var chapas = await _db.AssociacaoChapas.AsNoTracking().Include(c => c.Membros)
            .Where(c => chapaIds.Contains(c.ChapaId))
            .ToListAsync(ct);

        var nova = new EleicaoAssociacao
        {
            EleicaoId = Guid.NewGuid(),
            Titulo = origem.Titulo,
            Turno = turno,
            Desempate = desempate,
            AnteriorId = origem.EleicaoId,
            Situacao = SituacaoEleicao.Rascunho,
            Edital = origem.Edital,
            Comissao = origem.Comissao,
            CriadaEm = Agora
        };
        _db.AssociacaoEleicoes.Add(nova);

        var numero = 1;
        foreach (var c in chapaIds.Select(id => chapas.First(x => x.ChapaId == id)))
        {
            var copia = new ChapaAssociacao { ChapaId = Guid.NewGuid(), EleicaoId = nova.EleicaoId, Numero = numero++, Nome = c.Nome, Plano = c.Plano };
            _db.AssociacaoChapas.Add(copia);
            foreach (var m in c.Membros)
                _db.AssociacaoChapaMembros.Add(new ChapaMembro { ChapaId = copia.ChapaId, Cargo = m.Cargo, TreinadorId = m.TreinadorId });
        }

        // Mesmos eleitores da etapa anterior, todos sem voto.
        var eleitores = await _db.AssociacaoEleitores.AsNoTracking()
            .Where(x => x.EleicaoId == origem.EleicaoId)
            .Select(x => x.TreinadorId)
            .ToListAsync(ct);
        _db.AssociacaoEleitores.AddRange(eleitores.Select(id => new EleicaoEleitor { EleicaoId = nova.EleicaoId, TreinadorId = id }));

        await _db.SaveChangesAsync(ct);
        return (await GetAsync(nova.EleicaoId, true, ct))!;
    }

    private async Task<Dictionary<Guid, int>> VotosAsync(Guid eleicaoId, CancellationToken ct) =>
        await _db.AssociacaoVotos.AsNoTracking()
            .Where(v => v.EleicaoId == eleicaoId && v.ChapaId != null)
            .GroupBy(v => v.ChapaId!.Value)
            .Select(g => new { ChapaId = g.Key, Votos = g.Count() })
            .ToDictionaryAsync(v => v.ChapaId, v => v.Votos, ct);

    private async Task<Dictionary<Guid, string>> NomesAsync(IEnumerable<Guid> chapaIds, CancellationToken ct)
    {
        var ids = chapaIds.ToList();
        return await _db.AssociacaoChapas.AsNoTracking()
            .Where(c => ids.Contains(c.ChapaId))
            .ToDictionaryAsync(c => c.ChapaId, c => c.Nome, ct);
    }

    /// <summary>A chapa que passa de 50% dos válidos. O desempate do 1º turno não elege ninguém: só classifica.</summary>
    private static Guid? VencedoraPelaContagem(EleicaoAssociacao e, IReadOnlyDictionary<Guid, int> votos)
    {
        if (e.Turno == 1 && e.Desempate) return null;
        var validos = votos.Values.Sum();
        return votos.Where(v => v.Value * 2 > validos).Select(v => (Guid?)v.Key).FirstOrDefault();
    }
}
