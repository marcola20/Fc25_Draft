using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Trocas e reciclagem de figurinhas. Regra de ouro: só repetida sai da mão de alguém (quem dá precisa ter
/// pelo menos 2), então o álbum colado nunca perde figurinha e os selos continuam valendo.
/// </summary>
public partial class AlbumService
{
    /// <summary>Disparado quando uma proposta nasce ou é respondida, para o aviso no celular sair logo.</summary>
    public static event Action? TrocasMudaram;

    private sealed record Posse(Guid TreinadorId, Guid FigurinhaId, int Quantidade);

    private sealed record TrocaLida(
        Guid TrocaId, Guid DeId, string DeNome, Guid ParaId, string ParaNome, StatusTroca Status,
        DateTime CriadaEm, DateTime ExpiraEm, DateTime? RespondidaEm, string? Motivo, Guid? ContrapropostaDeId,
        List<(Guid FigurinhaId, bool Oferecida)> Itens);

    private async Task<int> TrocasEsperandoAsync(Guid treinadorId, CancellationToken ct)
    {
        var agora = Agora;
        return await _db.TrocasFigurinhas.AsNoTracking()
            .CountAsync(t => t.ParaTreinadorId == treinadorId && t.Status == StatusTroca.Pendente && t.ExpiraEm > agora, ct);
    }

    private async Task<List<Posse>> PossesAsync(Guid albumId, CancellationToken ct) =>
        await _db.FigurinhasDosTreinadores.AsNoTracking()
            .Where(f => f.Figurinha.AlbumId == albumId)
            .Select(f => new Posse(f.TreinadorId, f.FigurinhaId, f.Quantidade))
            .ToListAsync(ct);

    // ---------------------------------------------------------------- Leitura

    public async Task<TrocasDoTreinadorDto?> TrocasAsync(Guid treinadorId, CancellationToken ct)
    {
        var album = await AtivoAsync(ct);
        if (album is null) return null;

        var figurinhas = await FigurinhasAsync(album.AlbumId, ct);
        var porId = figurinhas.ToDictionary(f => f.FigurinhaId);
        var posses = await PossesAsync(album.AlbumId, ct);
        var pessoas = await PessoasAtivasAsync(posses.Select(p => p.TreinadorId).Where(id => id != treinadorId), ct);

        var minhas = posses.Where(p => p.TreinadorId == treinadorId).ToDictionary(p => p.FigurinhaId, p => p.Quantidade);
        var outros = posses.Where(p => p.TreinadorId != treinadorId && pessoas.ContainsKey(p.TreinadorId)).ToList();
        var repetidasDe = outros.Where(p => p.Quantidade >= 2).ToLookup(p => p.FigurinhaId, p => p.TreinadorId);

        var minhasRepetidas = minhas.Where(m => m.Value >= 2)
            .Select(m => new RepetidaDto(porId[m.Key], m.Value))
            .OrderBy(r => r.Figurinha.Numero)
            .ToList();
        var faltam = figurinhas.Where(f => !minhas.ContainsKey(f.FigurinhaId))
            .Select(f => new FaltaDto(f, repetidasDe[f.FigurinhaId]
                .Select(id => new PessoaDaTrocaDto(id, pessoas[id]))
                .OrderBy(p => p.Nome, StringComparer.CurrentCulture)
                .ToList()))
            .ToList();

        var agora = Agora;
        var trocas = (await TrocasLidasAsync(t => t.AlbumId == album.AlbumId
                                                  && (t.DeTreinadorId == treinadorId || t.ParaTreinadorId == treinadorId), ct))
            .Select(t => ParaDto(t, porId, agora))
            .ToList();

        return new TrocasDoTreinadorDto(
            album,
            minhasRepetidas,
            faltam,
            trocas.Where(t => t.Pendente && t.Para.TreinadorId == treinadorId).OrderBy(t => t.CriadaEm).ToList(),
            trocas.Where(t => t.Pendente && t.De.TreinadorId == treinadorId).OrderBy(t => t.CriadaEm).ToList(),
            trocas.Where(t => !t.Pendente).OrderByDescending(t => t.RespondidaEm ?? t.CriadaEm).Take(30).ToList(),
            Sugerir(minhas, outros, pessoas, porId),
            pessoas.Select(p => new PessoaDaTrocaDto(p.Key, p.Value)).OrderBy(p => p.Nome, StringComparer.CurrentCulture).ToList());
    }

    /// <summary>
    /// "Fulano tem 3 que você precisa e precisa de 2 suas": para cada pessoa, as repetidas dela que faltam no
    /// meu álbum e as minhas repetidas que faltam no dela. Melhor encaixe primeiro (quantas dá para trocar
    /// uma por uma), depois quem tem mais coisa para trocar.
    /// </summary>
    private static List<SugestaoDeTrocaDto> Sugerir(
        Dictionary<Guid, int> minhas, List<Posse> outros, Dictionary<Guid, string> pessoas, Dictionary<Guid, FigurinhaDto> porId)
    {
        var minhasRepetidas = minhas.Where(m => m.Value >= 2).Select(m => m.Key).ToList();
        return outros
            .GroupBy(p => p.TreinadorId)
            .Select(g =>
            {
                var tem = g.Select(p => p.FigurinhaId).ToHashSet();
                var eleTem = g.Where(p => p.Quantidade >= 2 && !minhas.ContainsKey(p.FigurinhaId))
                    .Select(p => porId[p.FigurinhaId]).OrderBy(f => f.Numero).ToList();
                var voceTem = minhasRepetidas.Where(id => !tem.Contains(id))
                    .Select(id => porId[id]).OrderBy(f => f.Numero).ToList();
                return new SugestaoDeTrocaDto(new PessoaDaTrocaDto(g.Key, pessoas[g.Key]), eleTem, voceTem);
            })
            .Where(s => s.EleTemQueVocePrecisa.Count > 0)
            .OrderByDescending(s => s.Encaixe)
            .ThenByDescending(s => s.EleTemQueVocePrecisa.Count + s.VoceTemQueElePrecisa.Count)
            .ThenBy(s => s.Pessoa.Nome, StringComparer.CurrentCulture)
            .Take(10)
            .ToList();
    }

    public async Task<MontarTrocaDto?> MontarTrocaAsync(Guid treinadorId, Guid outroId, CancellationToken ct)
    {
        var album = await AtivoAsync(ct);
        if (album is null || outroId == treinadorId) return null;

        var outro = (await PessoasAtivasAsync(new[] { outroId }, ct)).FirstOrDefault();
        if (outro.Key == Guid.Empty) return null;

        var figurinhas = await FigurinhasAsync(album.AlbumId, ct);
        var porId = figurinhas.ToDictionary(f => f.FigurinhaId);
        var posses = (await PossesAsync(album.AlbumId, ct)).Where(p => p.TreinadorId == treinadorId || p.TreinadorId == outroId).ToList();
        var minhas = posses.Where(p => p.TreinadorId == treinadorId).ToDictionary(p => p.FigurinhaId, p => p.Quantidade);
        var dele = posses.Where(p => p.TreinadorId == outroId).ToDictionary(p => p.FigurinhaId, p => p.Quantidade);

        List<RepetidaDto> Repetidas(Dictionary<Guid, int> de) => de.Where(p => p.Value >= 2)
            .Select(p => new RepetidaDto(porId[p.Key], p.Value)).OrderBy(r => r.Figurinha.Numero).ToList();

        return new MontarTrocaDto(
            new PessoaDaTrocaDto(outro.Key, outro.Value),
            Repetidas(minhas),
            Repetidas(dele),
            figurinhas.Where(f => !dele.ContainsKey(f.FigurinhaId)).Select(f => f.FigurinhaId).ToHashSet(),
            figurinhas.Where(f => !minhas.ContainsKey(f.FigurinhaId)).Select(f => f.FigurinhaId).ToHashSet());
    }

    // ---------------------------------------------------------------- Propor, recusar, cancelar, expirar

    public async Task<TrocaDto> ProporTrocaAsync(Guid deId, Guid paraId, IReadOnlyList<Guid> oferecidas, IReadOnlyList<Guid> pedidas,
        Guid? contrapropostaDe, CancellationToken ct)
    {
        if (deId == paraId) throw new InvalidOperationException("Escolha outra pessoa para trocar.");
        var dar = oferecidas.Distinct().ToList();
        var receber = pedidas.Distinct().ToList();
        if (dar.Count == 0 || receber.Count == 0)
            throw new InvalidOperationException("Escolha pelo menos uma repetida sua e uma dele.");
        if (dar.Intersect(receber).Any())
            throw new InvalidOperationException("A mesma figurinha não pode estar dos dois lados da troca.");
        if (dar.Count > AlbumFigurinhas.MaximoPorLadoDaTroca || receber.Count > AlbumFigurinhas.MaximoPorLadoDaTroca)
            throw new InvalidOperationException($"No máximo {AlbumFigurinhas.MaximoPorLadoDaTroca} figurinhas de cada lado.");

        var album = await AtivoAsync(ct) ?? throw new InvalidOperationException("O álbum ainda não foi lançado.");
        var pessoas = await PessoasAtivasAsync(new[] { deId, paraId }, ct);
        if (!pessoas.ContainsKey(paraId)) throw new InvalidOperationException("Essa pessoa não está mais na liga.");
        if (!pessoas.ContainsKey(deId)) throw new InvalidOperationException("Pessoa não encontrada.");

        var porId = (await FigurinhasAsync(album.AlbumId, ct)).ToDictionary(f => f.FigurinhaId);
        if (dar.Concat(receber).Any(id => !porId.ContainsKey(id)))
            throw new InvalidOperationException("Só figurinhas do álbum da temporada entram na troca.");

        var ids = dar.Concat(receber).ToList();
        var posses = await _db.FigurinhasDosTreinadores.AsNoTracking()
            .Where(f => (f.TreinadorId == deId || f.TreinadorId == paraId) && ids.Contains(f.FigurinhaId))
            .ToDictionaryAsync(f => (f.TreinadorId, f.FigurinhaId), f => f.Quantidade, ct);
        var falta = PrimeiraQueNaoERepetida(dar, deId, posses, porId, "Você")
                    ?? PrimeiraQueNaoERepetida(receber, paraId, posses, porId, pessoas[paraId]);
        if (falta is not null) throw new InvalidOperationException(falta);

        var agora = Agora;
        var troca = new TrocaFigurinhas
        {
            TrocaId = Guid.NewGuid(),
            AlbumId = album.AlbumId,
            DeTreinadorId = deId,
            ParaTreinadorId = paraId,
            Status = StatusTroca.Pendente,
            CriadaEm = agora,
            ExpiraEm = agora + AlbumFigurinhas.PrazoDaTroca,
            ContrapropostaDeId = contrapropostaDe
        };
        foreach (var id in dar) troca.Itens.Add(new TrocaFigurinhaItem { FigurinhaId = id, Oferecida = true });
        foreach (var id in receber) troca.Itens.Add(new TrocaFigurinhaItem { FigurinhaId = id, Oferecida = false });

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // Contraproposta fecha a original, mas só se ela ainda estiver esperando resposta de quem responde.
            if (contrapropostaDe is { } originalId)
            {
                var fechou = await _db.TrocasFigurinhas
                    .Where(t => t.TrocaId == originalId && t.Status == StatusTroca.Pendente && t.ExpiraEm > agora
                                && t.ParaTreinadorId == deId && t.DeTreinadorId == paraId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(t => t.Status, StatusTroca.Contraproposta)
                        .SetProperty(t => t.RespondidaEm, agora), ct);
                if (fechou == 0) throw new InvalidOperationException("Essa proposta não está mais esperando resposta.");
            }

            _db.TrocasFigurinhas.Add(troca);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        TrocasMudaram?.Invoke();
        return ParaDto(new TrocaLida(troca.TrocaId, deId, pessoas[deId], paraId, pessoas[paraId], troca.Status, troca.CriadaEm,
            troca.ExpiraEm, null, null, contrapropostaDe, troca.Itens.Select(i => (i.FigurinhaId, i.Oferecida)).ToList()), porId, agora);
    }

    public async Task RecusarTrocaAsync(Guid trocaId, Guid treinadorId, CancellationToken ct)
    {
        var agora = Agora;
        var mudou = await _db.TrocasFigurinhas
            .Where(t => t.TrocaId == trocaId && t.ParaTreinadorId == treinadorId && t.Status == StatusTroca.Pendente && t.ExpiraEm > agora)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, StatusTroca.Recusada).SetProperty(t => t.RespondidaEm, agora), ct);
        if (mudou == 0) throw new InvalidOperationException("Essa proposta não está mais esperando a sua resposta.");
        TrocasMudaram?.Invoke();
    }

    public async Task CancelarTrocaAsync(Guid trocaId, Guid treinadorId, CancellationToken ct)
    {
        var agora = Agora;
        var mudou = await _db.TrocasFigurinhas
            .Where(t => t.TrocaId == trocaId && t.DeTreinadorId == treinadorId && t.Status == StatusTroca.Pendente)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, StatusTroca.Cancelada).SetProperty(t => t.RespondidaEm, agora), ct);
        if (mudou == 0) throw new InvalidOperationException("Essa proposta já foi respondida.");
    }

    public async Task<int> ExpirarTrocasAsync(CancellationToken ct)
    {
        var agora = Agora;
        return await _db.TrocasFigurinhas
            .Where(t => t.Status == StatusTroca.Pendente && t.ExpiraEm <= agora)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, StatusTroca.Expirada).SetProperty(t => t.RespondidaEm, t => t.ExpiraEm), ct);
    }

    // ---------------------------------------------------------------- Aceitar

    public async Task<ResultadoDaTrocaDto> AceitarTrocaAsync(Guid trocaId, Guid treinadorId, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        var resultado = await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var pessoasDaTroca = await _db.TrocasFigurinhas.AsNoTracking()
                .Where(t => t.TrocaId == trocaId)
                .Select(t => new { t.DeTreinadorId, t.ParaTreinadorId })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Proposta não encontrada.");
            if (pessoasDaTroca.ParaTreinadorId != treinadorId)
                throw new InvalidOperationException("Só quem recebeu a proposta pode aceitar.");

            // Trava as duas pessoas sempre na mesma ordem: duas trocas cruzadas aceitas ao mesmo tempo não
            // ficam uma esperando a outra. Abrir pacote e reciclar travam a mesma linha, então ninguém mexe
            // nas figurinhas delas até o fim desta transação.
            foreach (var pessoa in new[] { pessoasDaTroca.DeTreinadorId, pessoasDaTroca.ParaTreinadorId }.OrderBy(id => id))
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"Treinadores\" WHERE \"TreinadorId\" = {pessoa} FOR UPDATE", ct);

            // Lida depois da trava: se outra aba aceitou antes, aqui já aparece aceita.
            var troca = await _db.TrocasFigurinhas.Include(t => t.Itens).FirstAsync(t => t.TrocaId == trocaId, ct);
            var agora = Agora;
            if (troca.Status != StatusTroca.Pendente)
                throw new InvalidOperationException(JaRespondida(troca.Status));

            if (troca.ExpiraEm <= agora)
                return await EncerrarAsync(troca, StatusTroca.Expirada, "Passou o prazo de 48 horas.", agora, tx, ct);

            var de = troca.DeTreinadorId;
            var para = troca.ParaTreinadorId;
            var ids = troca.Itens.Select(i => i.FigurinhaId).ToList();
            var posses = await _db.FigurinhasDosTreinadores
                .Where(f => (f.TreinadorId == de || f.TreinadorId == para) && ids.Contains(f.FigurinhaId))
                .ToDictionaryAsync(f => (f.TreinadorId, f.FigurinhaId), ct);

            var figurinhas = await FigurinhasAsync(troca.AlbumId, ct);
            var porId = figurinhas.ToDictionary(f => f.FigurinhaId);
            var nomes = await PessoasAtivasAsync(new[] { de, para }, ct, incluirInativos: true);
            var ativos = await PessoasAtivasAsync(new[] { de, para }, ct);
            var quantidades = posses.ToDictionary(p => p.Key, p => p.Value.Quantidade);
            // Quem saiu da liga não troca mais (propor já recusa; a proposta feita antes de sair também cai).
            var motivo = !ativos.ContainsKey(de) ? $"{nomes[de]} não está mais na liga."
                : !ativos.ContainsKey(para) ? "Você não está mais na liga."
                : PrimeiraQueNaoERepetida(troca.Itens.Where(i => i.Oferecida).Select(i => i.FigurinhaId).ToList(), de, quantidades, porId, nomes[de])
                         ?? PrimeiraQueNaoERepetida(troca.Itens.Where(i => !i.Oferecida).Select(i => i.FigurinhaId).ToList(), para, quantidades, porId, "Você");
            if (motivo is not null)
                return await EncerrarAsync(troca, StatusTroca.NaoValeMais, motivo, agora, tx, ct);

            // Move tudo: cada item sai de um e entra no outro. Quem não tinha a figurinha cola na hora (NOVA).
            var novasDe = new HashSet<Guid>();
            var novasPara = new HashSet<Guid>();
            var recebidasPara = new List<FigurinhaDto>();
            foreach (var item in troca.Itens)
            {
                var (sai, entra, novas) = item.Oferecida ? (de, para, novasPara) : (para, de, novasDe);
                posses[(sai, item.FigurinhaId)].Quantidade--;

                if (posses.TryGetValue((entra, item.FigurinhaId), out var tem))
                {
                    tem.Quantidade++;
                }
                else
                {
                    var nova = new FigurinhaDoTreinador
                    {
                        TreinadorId = entra,
                        FigurinhaId = item.FigurinhaId,
                        Quantidade = 1,
                        PrimeiraEm = agora,
                        Nova = true
                    };
                    _db.FigurinhasDosTreinadores.Add(nova);
                    posses[(entra, item.FigurinhaId)] = nova;
                    novas.Add(porId[item.FigurinhaId].TeamId);
                }

                if (item.Oferecida) recebidasPara.Add(porId[item.FigurinhaId]);
            }

            troca.Status = StatusTroca.Aceita;
            troca.RespondidaEm = agora;
            await _db.SaveChangesAsync(ct);

            // A figurinha que chegou pode fechar página ou o álbum, como na abertura de pacote.
            var selosPara = await GravarConquistasAsync(para, troca.AlbumId, figurinhas, novasPara, agora, ct);
            var selosDe = await GravarConquistasAsync(de, troca.AlbumId, figurinhas, novasDe, agora, ct);
            await tx.CommitAsync(ct);

            if (selosPara.Count + selosDe.Count > 0) ConquistasGravadas?.Invoke();
            return new ResultadoDaTrocaDto(true, $"Troca feita com {nomes[de]}! As figurinhas já estão no seu álbum.",
                recebidasPara.OrderBy(f => f.Numero).ToList(), selosPara);
        });

        TrocasMudaram?.Invoke();
        return resultado;
    }

    private async Task<ResultadoDaTrocaDto> EncerrarAsync(
        TrocaFigurinhas troca, StatusTroca status, string motivo, DateTime agora,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, CancellationToken ct)
    {
        troca.Status = status;
        troca.Motivo = motivo;
        troca.RespondidaEm = agora;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        var titulo = status == StatusTroca.Expirada ? "A proposta expirou" : "A troca não vale mais";
        return new ResultadoDaTrocaDto(false, $"{titulo}: {motivo}", Array.Empty<FigurinhaDto>(), Array.Empty<ConquistaAlbumDto>());
    }

    private static string JaRespondida(StatusTroca status) => status switch
    {
        StatusTroca.Aceita => "Essa proposta já foi aceita.",
        StatusTroca.Recusada => "Essa proposta já foi recusada.",
        StatusTroca.Cancelada => "Quem propôs cancelou essa troca.",
        StatusTroca.Expirada => "Essa proposta expirou.",
        StatusTroca.Contraproposta => "Essa proposta já teve contraproposta.",
        _ => "Essa proposta não vale mais."
    };

    /// <summary>
    /// A primeira figurinha da lista que <paramref name="dono"/> não tem mais repetida, já como frase
    /// ("Fulano não tem mais o Totti repetido"); nulo se todas ainda são repetidas.
    /// </summary>
    private static string? PrimeiraQueNaoERepetida(
        IReadOnlyList<Guid> ids, Guid dono, IReadOnlyDictionary<(Guid, Guid), int> quantidades,
        IReadOnlyDictionary<Guid, FigurinhaDto> porId, string quem)
    {
        foreach (var id in ids)
        {
            if (quantidades.GetValueOrDefault((dono, id)) >= 2) continue;
            var f = porId[id];
            var nome = f.Tipo == TipoFigurinha.Escudo ? $"o escudo do {f.TimeNome}" : $"o {f.NomeImpresso}";
            return $"{quem} não tem mais {nome} ({AlbumFigurinhas.NumeroImpresso(f.Numero)}) repetido.";
        }

        return null;
    }

    // ---------------------------------------------------------------- Reciclar

    public async Task ReciclarAsync(Guid treinadorId, IReadOnlyList<Guid> figurinhas, CancellationToken ct)
    {
        if (figurinhas.Count != AlbumFigurinhas.RepetidasPorPacote)
            throw new InvalidOperationException($"Escolha exatamente {AlbumFigurinhas.RepetidasPorPacote} repetidas.");

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Treinadores\" WHERE \"TreinadorId\" = {treinadorId} FOR UPDATE", ct);

            var albumId = await _db.Albuns.Where(a => a.Ativo).Select(a => (Guid?)a.AlbumId).FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("O álbum ainda não foi lançado.");

            var porFigurinha = figurinhas.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
            var ids = porFigurinha.Keys.ToList();
            var posses = await _db.FigurinhasDosTreinadores
                .Where(f => f.TreinadorId == treinadorId && f.Figurinha.AlbumId == albumId && ids.Contains(f.FigurinhaId))
                .Include(f => f.Figurinha)
                .ToDictionaryAsync(f => f.FigurinhaId, ct);

            foreach (var (id, quantas) in porFigurinha)
            {
                // A colada fica: só as cópias que sobram entram na reciclagem.
                if (!posses.TryGetValue(id, out var posse) || posse.Quantidade - quantas < 1)
                {
                    var nome = posse?.Figurinha.NomeImpresso ?? "uma das figurinhas";
                    throw new InvalidOperationException($"Você não tem tantas repetidas de {nome}.");
                }

                posse.Quantidade -= quantas;
            }

            var pacoteId = Guid.NewGuid();
            _db.PacotesGanhos.Add(new PacoteGanho
            {
                PacoteId = pacoteId,
                TreinadorId = treinadorId,
                AlbumId = albumId,
                Origem = PacoteGanho.OrigemReciclagem,
                Chave = AlbumFigurinhas.ChaveReciclagem(pacoteId),
                Motivo = AlbumFigurinhas.MotivoReciclagem,
                CriadoEm = Agora
            });
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    // ---------------------------------------------------------------- Apoio

    /// <summary>Nome das pessoas (só as ativas, salvo pedido): quem saiu da liga não aparece para trocar.</summary>
    private async Task<Dictionary<Guid, string>> PessoasAtivasAsync(IEnumerable<Guid> ids, CancellationToken ct, bool incluirInativos = false)
    {
        var lista = ids.Distinct().ToList();
        return await _db.Treinadores.AsNoTracking()
            .Where(t => lista.Contains(t.TreinadorId) && (incluirInativos || t.Ativo))
            .ToDictionaryAsync(t => t.TreinadorId, t => t.Nome, ct);
    }

    private async Task<List<TrocaLida>> TrocasLidasAsync(System.Linq.Expressions.Expression<Func<TrocaFigurinhas, bool>> filtro, CancellationToken ct)
    {
        var lidas = await _db.TrocasFigurinhas.AsNoTracking()
            .Where(filtro)
            .Select(t => new
            {
                t.TrocaId, t.DeTreinadorId, DeNome = t.De.Nome, t.ParaTreinadorId, ParaNome = t.Para.Nome, t.Status,
                t.CriadaEm, t.ExpiraEm, t.RespondidaEm, t.Motivo, t.ContrapropostaDeId,
                Itens = t.Itens.Select(i => new { i.FigurinhaId, i.Oferecida }).ToList()
            })
            .ToListAsync(ct);

        return lidas.Select(t => new TrocaLida(t.TrocaId, t.DeTreinadorId, t.DeNome, t.ParaTreinadorId, t.ParaNome, t.Status,
                t.CriadaEm, t.ExpiraEm, t.RespondidaEm, t.Motivo, t.ContrapropostaDeId,
                t.Itens.Select(i => (i.FigurinhaId, i.Oferecida)).ToList()))
            .ToList();
    }

    /// <summary>Proposta pendente que passou do prazo já aparece como expirada, mesmo antes da rodada que grava.</summary>
    private static TrocaDto ParaDto(TrocaLida t, IReadOnlyDictionary<Guid, FigurinhaDto> porId, DateTime agora)
    {
        var expirou = t.Status == StatusTroca.Pendente && t.ExpiraEm <= agora;
        return new TrocaDto(
            t.TrocaId,
            new PessoaDaTrocaDto(t.DeId, t.DeNome),
            new PessoaDaTrocaDto(t.ParaId, t.ParaNome),
            expirou ? StatusTroca.Expirada : t.Status,
            t.CriadaEm,
            t.ExpiraEm,
            expirou ? t.ExpiraEm : t.RespondidaEm,
            t.Motivo,
            t.ContrapropostaDeId,
            t.Itens.Where(i => i.Oferecida && porId.ContainsKey(i.FigurinhaId)).Select(i => porId[i.FigurinhaId]).OrderBy(f => f.Numero).ToList(),
            t.Itens.Where(i => !i.Oferecida && porId.ContainsKey(i.FigurinhaId)).Select(i => porId[i.FigurinhaId]).OrderBy(f => f.Numero).ToList());
    }
}
