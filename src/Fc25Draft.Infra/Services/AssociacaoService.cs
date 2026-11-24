using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>Eleição da Associação de Treinadores (página /associacao, admin em /admin/associacao).</summary>
public partial class AssociacaoService : IAssociacaoService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public AssociacaoService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    private DateTime Agora => _time.GetUtcNow().UtcDateTime;

    // ── Página ───────────────────────────────────────────────────────────────

    public async Task<EleicaoDto?> GetAtualAsync(CancellationToken ct)
    {
        var id = await _db.AssociacaoEleicoes.AsNoTracking()
            .Where(e => e.Situacao != SituacaoEleicao.Rascunho)
            .OrderByDescending(e => e.CriadaEm)
            .Select(e => (Guid?)e.EleicaoId)
            .FirstOrDefaultAsync(ct);

        return id is Guid eleicaoId ? await GetAsync(eleicaoId, false, ct) : null;
    }

    public async Task<DiretoriaAssociacaoDto?> GetDiretoriaAsync(CancellationToken ct)
    {
        var eleicao = await _db.AssociacaoEleicoes.AsNoTracking()
            .Where(e => e.Situacao == SituacaoEleicao.Resultado && e.ChapaVencedoraId != null)
            .OrderByDescending(e => e.ResultadoEm)
            .FirstOrDefaultAsync(ct);
        if (eleicao is null) return null;

        var dto = await GetAsync(eleicao.EleicaoId, false, ct);
        var chapa = dto?.Chapas.FirstOrDefault(c => c.ChapaId == eleicao.ChapaVencedoraId);
        return chapa is null ? null : new DiretoriaAssociacaoDto(eleicao.EleicaoId, eleicao.Titulo, eleicao.ResultadoEm!.Value, chapa);
    }

    public async Task<IReadOnlyList<EleicaoEleitorDto>> EleitoresAsync(Guid eleicaoId, CancellationToken ct)
    {
        var clubes = await ClubesAtuaisAsync(ct);
        var eleitores = await _db.AssociacaoEleitores.AsNoTracking()
            .Where(e => e.EleicaoId == eleicaoId)
            .Select(e => new { e.TreinadorId, e.Treinador.Nome, Votou = e.VotouEm != null })
            .ToListAsync(ct);

        return eleitores
            .Select(e =>
            {
                clubes.TryGetValue(e.TreinadorId, out var clube);
                return new EleicaoEleitorDto(e.TreinadorId, e.Nome, clube.Papel, clube.Time, e.Votou);
            })
            .OrderBy(e => e.Time ?? "~")
            .ThenBy(e => e.Papel)
            .ThenBy(e => e.Nome)
            .ToList();
    }

    public async Task VotarAsync(Guid eleicaoId, Guid treinadorId, Guid? chapaId, CancellationToken ct)
    {
        var eleicao = await _db.AssociacaoEleicoes.AsNoTracking().FirstOrDefaultAsync(e => e.EleicaoId == eleicaoId, ct)
            ?? throw new InvalidOperationException("Eleição não encontrada.");
        if (eleicao.Situacao != SituacaoEleicao.Votacao) throw new InvalidOperationException("A votação não está aberta.");

        var agora = Agora;
        if (eleicao.AbreEm is DateTime abre && agora < abre)
            throw new InvalidOperationException("A urna ainda não abriu.");
        if (eleicao.FechaEm is DateTime fecha && agora >= fecha)
            throw new InvalidOperationException("A urna já fechou.");

        if (chapaId is Guid id && !await _db.AssociacaoChapas.AnyAsync(c => c.ChapaId == id && c.EleicaoId == eleicaoId, ct))
            throw new InvalidOperationException("Essa chapa não está nesta eleição.");

        var eleitor = await _db.AssociacaoEleitores.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EleicaoId == eleicaoId && e.TreinadorId == treinadorId, ct)
            ?? throw new InvalidOperationException("Você não está na lista de eleitores desta eleição. Fale com a comissão eleitoral.");
        if (eleitor.VotouEm is not null) throw new InvalidOperationException("Você já votou. O voto não pode ser trocado.");

        // Marca que votou e põe o voto na urna juntos: ou os dois, ou nenhum. A marcação só pega quem ainda
        // não votou, então dois cliques ao mesmo tempo não viram dois votos.
        var estrategia = _db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var marcou = await _db.AssociacaoEleitores
                .Where(e => e.EleicaoId == eleicaoId && e.TreinadorId == treinadorId && e.VotouEm == null)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.VotouEm, agora), ct);
            if (marcou == 0) throw new InvalidOperationException("Você já votou. O voto não pode ser trocado.");

            _db.AssociacaoVotos.Add(new EleicaoVoto { VotoId = Guid.NewGuid(), EleicaoId = eleicaoId, ChapaId = chapaId });
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    public async Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct)
    {
        var eleicoes = await _db.AssociacaoEleicoes.AsNoTracking()
            .Where(e => e.Situacao != SituacaoEleicao.Rascunho)
            .Select(e => new { e.Titulo, e.Turno, e.Desempate, e.VotacaoAbertaEm, e.ResultadoEm, e.ChapaVencedoraId })
            .ToListAsync(ct);

        var nomes = await NomesAsync(eleicoes.Where(e => e.ChapaVencedoraId != null).Select(e => e.ChapaVencedoraId!.Value), ct);

        var noticias = new List<PlantaoNoticiaDto>();
        foreach (var e in eleicoes)
        {
            var etapa = CargosAssociacao.Etapa(e.Turno, e.Desempate);
            if (e.VotacaoAbertaEm is DateTime aberta)
                noticias.Add(new PlantaoNoticiaDto(aberta, PlantaoCategoria.Novidade, "🗳️",
                    $"Associação de Treinadores: votação aberta ({etapa})",
                    "Treinadores e auxiliares escolhem a Diretoria Executiva. Voto secreto, no site.", "/associacao", null));

            if (e.ResultadoEm is DateTime resultado)
                noticias.Add(e.ChapaVencedoraId is Guid v && nomes.TryGetValue(v, out var nome)
                    ? new PlantaoNoticiaDto(resultado, PlantaoCategoria.Novidade, "🏛️",
                        $"Chapa {nome} eleita para a Associação de Treinadores", $"{e.Titulo} · {etapa}", "/associacao", null)
                    : new PlantaoNoticiaDto(resultado, PlantaoCategoria.Novidade, "🗳️",
                        $"Associação de Treinadores: resultado do {etapa}", "Ainda sem chapa eleita. Veja a próxima etapa.", "/associacao", null));
        }

        return noticias;
    }

    // ── Admin ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<EleicaoResumoDto>> ListAsync(CancellationToken ct) =>
        await _db.AssociacaoEleicoes.AsNoTracking()
            .OrderByDescending(e => e.CriadaEm)
            .Select(e => new EleicaoResumoDto(e.EleicaoId, e.Titulo, e.Turno, e.Desempate, e.Situacao, e.CriadaEm))
            .ToListAsync(ct);

    public async Task<EleicaoDto?> GetAsync(Guid eleicaoId, bool comContagem, CancellationToken ct)
    {
        var e = await _db.AssociacaoEleicoes.AsNoTracking()
            .Include(x => x.Chapas).ThenInclude(c => c.Membros).ThenInclude(m => m.Treinador)
            .FirstOrDefaultAsync(x => x.EleicaoId == eleicaoId, ct);
        if (e is null) return null;

        var eleitores = await _db.AssociacaoEleitores.AsNoTracking().CountAsync(x => x.EleicaoId == eleicaoId, ct);
        var votaram = await _db.AssociacaoEleitores.AsNoTracking().CountAsync(x => x.EleicaoId == eleicaoId && x.VotouEm != null, ct);

        // A contagem só aparece com a urna fechada: no resultado para todos, na apuração só para o admin.
        var mostraContagem = e.Situacao == SituacaoEleicao.Resultado || (comContagem && e.Situacao == SituacaoEleicao.Apuracao);
        Dictionary<Guid, int> porChapa = new();
        EleicaoApuracaoDto? apuracao = null;
        if (mostraContagem)
        {
            var votos = await _db.AssociacaoVotos.AsNoTracking()
                .Where(v => v.EleicaoId == eleicaoId)
                .GroupBy(v => v.ChapaId)
                .Select(g => new { ChapaId = g.Key, Votos = g.Count() })
                .ToListAsync(ct);
            porChapa = votos.Where(v => v.ChapaId != null).ToDictionary(v => v.ChapaId!.Value, v => v.Votos);
            var brancos = votos.Where(v => v.ChapaId == null).Sum(v => v.Votos);
            var validos = porChapa.Values.Sum();
            apuracao = new EleicaoApuracaoDto(validos, brancos, Math.Max(0, eleitores - validos - brancos));
        }

        var clubes = await ClubesAtuaisAsync(ct);
        var chapas = e.Chapas
            .OrderBy(c => c.Numero)
            .Select(c => new ChapaDto(
                c.ChapaId, c.Numero, c.Nome, c.Plano,
                c.Membros.OrderBy(m => m.Cargo)
                    .Select(m => new ChapaMembroDto(m.Cargo, m.TreinadorId, m.Treinador.Nome,
                        clubes.TryGetValue(m.TreinadorId, out var clube) ? clube.Time : null))
                    .ToList(),
                mostraContagem ? porChapa.GetValueOrDefault(c.ChapaId) : null))
            .ToList();

        // No resultado, o que ele decide; na apuração (admin), a prévia do que a publicação vai decidir.
        DesfechoEleicaoDto? desfecho = null;
        if (mostraContagem)
            desfecho = await DesfechoAsync(e, e.Situacao == SituacaoEleicao.Resultado
                ? e.ChapaVencedoraId
                : VencedoraPelaContagem(e, porChapa), ct);

        return new EleicaoDto(e.EleicaoId, e.Titulo, e.Turno, e.Desempate, e.Situacao, e.InscricoesAte, e.AbreEm, e.FechaEm,
            e.Edital, e.Comissao, chapas, eleitores, votaram, apuracao,
            e.Situacao == SituacaoEleicao.Resultado ? e.ChapaVencedoraId : null, e.CriadaEm, e.ResultadoEm, desfecho);
    }

    public async Task<EleicaoDto> CriarAsync(EleicaoSalvarRequest request, CancellationToken ct)
    {
        Validar(request);
        var eleicao = new EleicaoAssociacao
        {
            EleicaoId = Guid.NewGuid(),
            Situacao = SituacaoEleicao.Rascunho,
            CriadaEm = Agora
        };
        Aplicar(eleicao, request);
        _db.AssociacaoEleicoes.Add(eleicao);
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(eleicao.EleicaoId, true, ct))!;
    }

    public async Task<EleicaoDto> SalvarAsync(Guid eleicaoId, EleicaoSalvarRequest request, CancellationToken ct)
    {
        Validar(request);
        var eleicao = await BuscarAsync(eleicaoId, ct);
        if (eleicao.Situacao == SituacaoEleicao.Resultado)
            throw new InvalidOperationException("Eleição com resultado publicado não muda mais.");
        Aplicar(eleicao, request);
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(eleicaoId, true, ct))!;
    }

    public async Task ExcluirAsync(Guid eleicaoId, CancellationToken ct)
    {
        var eleicao = await BuscarAsync(eleicaoId, ct);
        if (await _db.AssociacaoVotos.AnyAsync(v => v.EleicaoId == eleicaoId, ct))
            throw new InvalidOperationException("Essa eleição já tem voto: não dá para excluir.");
        _db.AssociacaoEleicoes.Remove(eleicao);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SalvarChapaAsync(Guid eleicaoId, Guid? chapaId, ChapaSalvarRequest request, CancellationToken ct)
    {
        var eleicao = await BuscarAsync(eleicaoId, ct);
        ExigirMontagem(eleicao);

        var nome = (request.Nome ?? "").Trim();
        if (nome.Length == 0) throw new ArgumentException("Dê um nome para a chapa.");
        if (nome.Length > 120) throw new ArgumentException("Nome da chapa muito longo (máximo 120 letras).");

        // Cada pessoa num cargo só, e numa chapa só (Art. 5º, II e III).
        var membros = request.Membros.Where(m => m.Value != Guid.Empty).ToDictionary(m => m.Key, m => m.Value);
        if (membros.Values.Distinct().Count() != membros.Count)
            throw new InvalidOperationException("A mesma pessoa está em dois cargos da chapa.");

        var outras = await _db.AssociacaoChapaMembros.AsNoTracking()
            .Where(m => m.Chapa.EleicaoId == eleicaoId && m.ChapaId != chapaId && membros.Values.Contains(m.TreinadorId))
            .Select(m => new { m.Treinador.Nome, Chapa = m.Chapa.Nome })
            .ToListAsync(ct);
        if (outras.Count > 0)
            throw new InvalidOperationException(
                $"{string.Join(", ", outras.Select(o => $"{o.Nome} (chapa {o.Chapa})"))} já está em outra chapa. Cada candidato entra em uma chapa só.");

        var ids = membros.Values.ToList();
        var existem = await _db.Treinadores.CountAsync(t => ids.Contains(t.TreinadorId), ct);
        if (existem != ids.Count) throw new InvalidOperationException("Tem treinador que não existe no cadastro.");

        ChapaAssociacao chapa;
        if (chapaId is Guid id)
        {
            chapa = await _db.AssociacaoChapas.Include(c => c.Membros).FirstOrDefaultAsync(c => c.ChapaId == id && c.EleicaoId == eleicaoId, ct)
                ?? throw new InvalidOperationException("Chapa não encontrada.");

            // Cargo é a chave do membro: troca a pessoa no lugar, tira quem saiu.
            foreach (var m in chapa.Membros.ToList())
            {
                if (membros.Remove(m.Cargo, out var novo)) m.TreinadorId = novo;
                else _db.AssociacaoChapaMembros.Remove(m);
            }
        }
        else
        {
            var numero = (await _db.AssociacaoChapas.Where(c => c.EleicaoId == eleicaoId).MaxAsync(c => (int?)c.Numero, ct) ?? 0) + 1;
            chapa = new ChapaAssociacao { ChapaId = Guid.NewGuid(), EleicaoId = eleicaoId, Numero = numero };
            _db.AssociacaoChapas.Add(chapa);
        }

        chapa.Nome = nome;
        chapa.Plano = (request.Plano ?? "").TrimEnd();
        foreach (var (cargo, treinadorId) in membros)
            _db.AssociacaoChapaMembros.Add(new ChapaMembro { ChapaId = chapa.ChapaId, Cargo = cargo, TreinadorId = treinadorId });

        await _db.SaveChangesAsync(ct);
    }

    public async Task ExcluirChapaAsync(Guid chapaId, CancellationToken ct)
    {
        var chapa = await _db.AssociacaoChapas.Include(c => c.Eleicao).FirstOrDefaultAsync(c => c.ChapaId == chapaId, ct)
            ?? throw new InvalidOperationException("Chapa não encontrada.");
        ExigirMontagem(chapa.Eleicao);

        var eleicaoId = chapa.EleicaoId;
        _db.AssociacaoChapas.Remove(chapa);
        await _db.SaveChangesAsync(ct);

        // Renumera para a cédula não ficar com buraco (1, 3...).
        var restantes = await _db.AssociacaoChapas.Where(c => c.EleicaoId == eleicaoId).OrderBy(c => c.Numero).ToListAsync(ct);
        for (var i = 0; i < restantes.Count; i++) restantes[i].Numero = -(i + 1);
        await _db.SaveChangesAsync(ct);
        foreach (var c in restantes) c.Numero = -c.Numero;
        await _db.SaveChangesAsync(ct);
    }

    public async Task PublicarEditalAsync(Guid eleicaoId, CancellationToken ct)
    {
        var eleicao = await BuscarAsync(eleicaoId, ct);
        if (eleicao.Situacao != SituacaoEleicao.Rascunho) throw new InvalidOperationException("O edital já está no ar.");

        eleicao.Situacao = SituacaoEleicao.Inscricoes;
        await _db.SaveChangesAsync(ct);

        if (!await _db.AssociacaoEleitores.AnyAsync(e => e.EleicaoId == eleicaoId, ct))
            await RefazerEleitoresAsync(eleicaoId, ct);
    }

    public async Task<int> RefazerEleitoresAsync(Guid eleicaoId, CancellationToken ct)
    {
        var eleicao = await BuscarAsync(eleicaoId, ct);
        ExigirMontagem(eleicao);

        // Art. 8º: treinadores e auxiliares em exercício na liga (ativos e com clube hoje).
        var aptos = await _db.TreinadorPassagens.AsNoTracking()
            .Where(p => p.Ate == null && p.Treinador.Ativo)
            .Select(p => p.TreinadorId)
            .Distinct()
            .ToListAsync(ct);

        _db.AssociacaoEleitores.RemoveRange(await _db.AssociacaoEleitores.Where(e => e.EleicaoId == eleicaoId).ToListAsync(ct));
        _db.AssociacaoEleitores.AddRange(aptos.Select(id => new EleicaoEleitor { EleicaoId = eleicaoId, TreinadorId = id }));
        await _db.SaveChangesAsync(ct);
        return aptos.Count;
    }

    public async Task AdicionarEleitorAsync(Guid eleicaoId, Guid treinadorId, CancellationToken ct)
    {
        ExigirMontagem(await BuscarAsync(eleicaoId, ct));
        if (await _db.AssociacaoEleitores.AnyAsync(e => e.EleicaoId == eleicaoId && e.TreinadorId == treinadorId, ct)) return;
        if (!await _db.Treinadores.AnyAsync(t => t.TreinadorId == treinadorId, ct))
            throw new InvalidOperationException("Treinador não encontrado.");

        _db.AssociacaoEleitores.Add(new EleicaoEleitor { EleicaoId = eleicaoId, TreinadorId = treinadorId });
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoverEleitorAsync(Guid eleicaoId, Guid treinadorId, CancellationToken ct)
    {
        ExigirMontagem(await BuscarAsync(eleicaoId, ct));
        var eleitor = await _db.AssociacaoEleitores.FirstOrDefaultAsync(e => e.EleicaoId == eleicaoId && e.TreinadorId == treinadorId, ct);
        if (eleitor is null) return;
        _db.AssociacaoEleitores.Remove(eleitor);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AbrirVotacaoAsync(Guid eleicaoId, CancellationToken ct)
    {
        var eleicao = await _db.AssociacaoEleicoes.Include(e => e.Chapas).ThenInclude(c => c.Membros)
            .FirstOrDefaultAsync(e => e.EleicaoId == eleicaoId, ct)
            ?? throw new InvalidOperationException("Eleição não encontrada.");
        if (eleicao.Situacao != SituacaoEleicao.Inscricoes)
            throw new InvalidOperationException("Publique o edital antes de abrir a votação.");
        if (eleicao.Chapas.Count == 0) throw new InvalidOperationException("Nenhuma chapa inscrita.");

        var incompletas = eleicao.Chapas
            .Where(c => c.Membros.Select(m => m.Cargo).Distinct().Count() < CargosAssociacao.Todos.Count)
            .Select(c => c.Nome)
            .ToList();
        if (incompletas.Count > 0)
            throw new InvalidOperationException($"Chapa sem todos os cargos (Art. 5º, I): {string.Join(", ", incompletas)}.");

        if (!await _db.AssociacaoEleitores.AnyAsync(e => e.EleicaoId == eleicaoId, ct))
            throw new InvalidOperationException("A lista de eleitores está vazia.");

        eleicao.Situacao = SituacaoEleicao.Votacao;
        eleicao.VotacaoAbertaEm ??= Agora;
        await _db.SaveChangesAsync(ct);
    }

    public async Task EncerrarAsync(Guid eleicaoId, CancellationToken ct)
    {
        var eleicao = await BuscarAsync(eleicaoId, ct);
        if (eleicao.Situacao != SituacaoEleicao.Votacao) throw new InvalidOperationException("A votação não está aberta.");
        eleicao.Situacao = SituacaoEleicao.Apuracao;
        eleicao.EncerradaEm = Agora;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReabrirAsync(Guid eleicaoId, CancellationToken ct)
    {
        var eleicao = await BuscarAsync(eleicaoId, ct);
        if (eleicao.Situacao != SituacaoEleicao.Apuracao) throw new InvalidOperationException("Só dá para reabrir na apuração.");
        eleicao.Situacao = SituacaoEleicao.Votacao;
        eleicao.EncerradaEm = null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task VoltarAsync(Guid eleicaoId, CancellationToken ct)
    {
        var eleicao = await BuscarAsync(eleicaoId, ct);
        if (await _db.AssociacaoVotos.AnyAsync(v => v.EleicaoId == eleicaoId, ct))
            throw new InvalidOperationException("Já tem voto na urna: não dá para voltar.");

        switch (eleicao.Situacao)
        {
            case SituacaoEleicao.Votacao:
                eleicao.Situacao = SituacaoEleicao.Inscricoes;
                eleicao.VotacaoAbertaEm = null;
                break;
            case SituacaoEleicao.Inscricoes:
                eleicao.Situacao = SituacaoEleicao.Rascunho;
                break;
            default:
                throw new InvalidOperationException("Não há passo para voltar.");
        }
        await _db.SaveChangesAsync(ct);
    }

    // ── Apoio ────────────────────────────────────────────────────────────────

    private async Task<EleicaoAssociacao> BuscarAsync(Guid eleicaoId, CancellationToken ct) =>
        await _db.AssociacaoEleicoes.FirstOrDefaultAsync(e => e.EleicaoId == eleicaoId, ct)
            ?? throw new InvalidOperationException("Eleição não encontrada.");

    /// <summary>Chapas e eleitores só mudam antes de a urna abrir.</summary>
    private static void ExigirMontagem(EleicaoAssociacao eleicao)
    {
        if (eleicao.Situacao is not (SituacaoEleicao.Rascunho or SituacaoEleicao.Inscricoes))
            throw new InvalidOperationException("Com a votação aberta, chapas e eleitores não mudam mais. Volte para as inscrições (só sem voto).");
    }

    private static void Validar(EleicaoSalvarRequest request)
    {
        var titulo = (request.Titulo ?? "").Trim();
        if (titulo.Length == 0) throw new ArgumentException("Dê um título para a eleição.");
        if (titulo.Length > 160) throw new ArgumentException("Título muito longo (máximo 160 letras).");
        if ((request.Comissao ?? "").Trim().Length > 500) throw new ArgumentException("Comissão muito longa (máximo 500 letras).");
        if (request.AbreEm is DateTime abre && request.FechaEm is DateTime fecha && fecha <= abre)
            throw new ArgumentException("O encerramento tem que ser depois da abertura.");
    }

    private static void Aplicar(EleicaoAssociacao e, EleicaoSalvarRequest r)
    {
        e.Titulo = r.Titulo.Trim();
        e.InscricoesAte = r.InscricoesAte;
        e.AbreEm = r.AbreEm;
        e.FechaEm = r.FechaEm;
        e.Edital = (r.Edital ?? "").TrimEnd();
        e.Comissao = (r.Comissao ?? "").Trim();
    }

    private async Task<Dictionary<Guid, (PapelTreinador? Papel, string? Time)>> ClubesAtuaisAsync(CancellationToken ct) =>
        (await _db.TreinadorPassagens.AsNoTracking()
            .Where(p => p.Ate == null)
            .Select(p => new { p.TreinadorId, p.Papel, p.Time.TeamName })
            .ToListAsync(ct))
        .GroupBy(p => p.TreinadorId)
        .ToDictionary(g => g.Key, g => ((PapelTreinador?)g.First().Papel, (string?)g.First().TeamName));
}
