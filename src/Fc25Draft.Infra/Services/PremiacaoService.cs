using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Premiação por temporada: o admin cadastra os valores (normalmente copiando a temporada
/// anterior) e, com a competição encerrada, credita tudo de uma vez no caixa dos times.
/// </summary>
public class PremiacaoService : IPremiacaoService
{
    private const string OrigemLedger = "PREMIACAO";

    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public PremiacaoService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    // ── Cadastro ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PremiacaoDto>> ListAsync(CancellationToken ct)
    {
        var premiacoes = await _db.Premiacoes
            .AsNoTracking()
            .Include(p => p.Itens)
            .OrderByDescending(p => p.Temporada)
            .ToListAsync(ct);

        return premiacoes.Select(ToDto).ToArray();
    }

    public async Task<PremiacaoDto?> GetAsync(Guid premiacaoId, CancellationToken ct)
    {
        var premiacao = await _db.Premiacoes.AsNoTracking().Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.PremiacaoId == premiacaoId, ct);

        return premiacao is null ? null : ToDto(premiacao);
    }

    public async Task<PremiacaoDto?> GetPorTemporadaAsync(int temporada, CancellationToken ct)
    {
        var premiacao = await _db.Premiacoes.AsNoTracking().Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.Temporada == temporada, ct);

        return premiacao is null ? null : ToDto(premiacao);
    }

    public async Task<PremiacaoDto?> GetAtualAsync(CancellationToken ct)
    {
        var premiacao = await _db.Premiacoes.AsNoTracking().Include(p => p.Itens)
            .OrderByDescending(p => p.Temporada)
            .FirstOrDefaultAsync(ct);

        return premiacao is null ? null : ToDto(premiacao);
    }

    public async Task<PremiacaoDto> CriarAsync(PremiacaoCriarRequest request, CancellationToken ct)
    {
        if (request.Temporada < 1900 || request.Temporada > 2999)
            throw new ArgumentException("Temporada inválida.");

        if (await _db.Premiacoes.AnyAsync(p => p.Temporada == request.Temporada, ct))
            throw new InvalidOperationException($"A temporada {request.Temporada} já tem premiação cadastrada.");

        var agora = _time.GetUtcNow().UtcDateTime;
        var premiacao = new Premiacao
        {
            PremiacaoId = Guid.NewGuid(),
            Temporada = request.Temporada,
            Nome = string.IsNullOrWhiteSpace(request.Nome) ? $"Premiação {request.Temporada}" : request.Nome.Trim(),
            CriadoEm = agora,
            AtualizadoEm = agora
        };

        if (request.CopiarDe is Guid origemId)
        {
            var origem = await _db.Premiacoes.AsNoTracking().Include(p => p.Itens)
                .FirstOrDefaultAsync(p => p.PremiacaoId == origemId, ct)
                ?? throw new InvalidOperationException("Premiação de origem não encontrada.");

            foreach (var item in origem.Itens)
                premiacao.Itens.Add(new PremiacaoItem
                {
                    PremiacaoItemId = Guid.NewGuid(),
                    PremiacaoId = premiacao.PremiacaoId,
                    Tipo = item.Tipo,
                    Divisao = item.Divisao,
                    PosicaoDe = item.PosicaoDe,
                    PosicaoAte = item.PosicaoAte,
                    Fase = item.Fase,
                    Valor = item.Valor
                });
        }

        _db.Premiacoes.Add(premiacao);
        await _db.SaveChangesAsync(ct);

        return ToDto(premiacao);
    }

    public async Task<PremiacaoDto> SalvarItensAsync(Guid premiacaoId, IReadOnlyList<PremiacaoItemInput> itens, CancellationToken ct)
    {
        var premiacao = await _db.Premiacoes.Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.PremiacaoId == premiacaoId, ct)
            ?? throw new InvalidOperationException("Premiação não encontrada.");

        foreach (var item in itens)
        {
            if (item.Valor < 0)
                throw new ArgumentException("O valor do prêmio não pode ser negativo.");

            if (item.Tipo == TipoCompetition.Liga)
            {
                if (item.PosicaoDe is null or < 1)
                    throw new ArgumentException("Prêmio da Liga precisa de posição.");
                if (item.PosicaoAte is not null && item.PosicaoAte < item.PosicaoDe)
                    throw new ArgumentException("A faixa de posições está invertida.");
            }
            else if (item.Fase is null)
            {
                throw new ArgumentException("Prêmio de Copa/Supercopa precisa de fase.");
            }
        }

        _db.PremiacaoItens.RemoveRange(premiacao.Itens);
        premiacao.Itens.Clear();

        // Pelo DbSet: item novo com Id preenchido dentro de uma premiação já salva
        // seria tratado como alteração de uma linha que não existe.
        foreach (var item in itens)
            _db.PremiacaoItens.Add(new PremiacaoItem
            {
                PremiacaoItemId = Guid.NewGuid(),
                PremiacaoId = premiacao.PremiacaoId,
                Tipo = item.Tipo,
                Divisao = item.Tipo == TipoCompetition.Liga ? item.Divisao : null,
                PosicaoDe = item.Tipo == TipoCompetition.Liga ? item.PosicaoDe : null,
                PosicaoAte = item.Tipo == TipoCompetition.Liga ? item.PosicaoAte ?? item.PosicaoDe : null,
                Fase = item.Tipo == TipoCompetition.Liga ? null : item.Fase,
                Valor = item.Valor
            });

        premiacao.AtualizadoEm = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        return (await GetAsync(premiacaoId, ct))!;
    }

    public async Task RenomearAsync(Guid premiacaoId, string nome, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Informe um nome.");

        var premiacao = await _db.Premiacoes.FirstOrDefaultAsync(p => p.PremiacaoId == premiacaoId, ct)
            ?? throw new InvalidOperationException("Premiação não encontrada.");

        premiacao.Nome = nome.Trim();
        premiacao.AtualizadoEm = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ExcluirAsync(Guid premiacaoId, CancellationToken ct)
    {
        var premiacao = await _db.Premiacoes.FirstOrDefaultAsync(p => p.PremiacaoId == premiacaoId, ct)
            ?? throw new InvalidOperationException("Premiação não encontrada.");

        if (await _db.PremiacaoPagamentos.AnyAsync(p => p.PremiacaoId == premiacaoId, ct))
            throw new InvalidOperationException("Essa premiação já pagou competição: estorne o pagamento antes de excluir.");

        _db.Premiacoes.Remove(premiacao);
        await _db.SaveChangesAsync(ct);
    }

    // ── Prévia e pagamento ─────────────────────────────────────────────────

    public async Task<IReadOnlyList<PremiacaoPreviaDto>> ListPreviasAsync(int temporada, CancellationToken ct)
    {
        var ligas = await _db.Ligas.AsNoTracking()
            .Where(l => l.Temporada == temporada)
            .OrderBy(l => l.Tipo).ThenBy(l => l.Divisao)
            .Select(l => l.LigaId)
            .ToListAsync(ct);

        var previas = new List<PremiacaoPreviaDto>(ligas.Count);
        foreach (var ligaId in ligas)
            if (await GetPreviaAsync(ligaId, ct) is PremiacaoPreviaDto previa)
                previas.Add(previa);

        return previas;
    }

    public async Task<PremiacaoPreviaDto?> GetPreviaAsync(Guid ligaId, CancellationToken ct)
    {
        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(l => l.LigaId == ligaId, ct);
        if (liga is null) return null;

        var pagamentos = await _db.PremiacaoPagamentos.AsNoTracking()
            .Where(p => p.LigaId == ligaId)
            .Include(p => p.Time)
            .ToListAsync(ct);

        var encerrada = liga.Status == LigaStatus.Encerrada;

        // Já pago: a prévia mostra exatamente o que foi creditado.
        if (pagamentos.Count > 0)
            return new PremiacaoPreviaDto(
                liga.LigaId, liga.Nome, liga.Temporada, liga.Tipo, liga.Divisao,
                encerrada, true, pagamentos.Max(p => p.PagoEm), pagamentos[0].PremiacaoId, null,
                pagamentos.OrderByDescending(p => p.Valor)
                    .Select(p => new PremiacaoLinhaDto(p.TimeId, p.Time.TeamName, p.Motivo, p.Valor))
                    .ToArray());

        if (liga.Temporada is not int temporada)
            return Impedida(liga, encerrada, "A competição não tem temporada definida.");

        var premiacao = await _db.Premiacoes.AsNoTracking().Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.Temporada == temporada, ct);

        if (premiacao is null)
            return Impedida(liga, encerrada, $"Nenhuma premiação cadastrada para a temporada {temporada}.");

        var itens = premiacao.Itens
            .Where(i => i.Tipo == liga.Tipo && (liga.Tipo != TipoCompetition.Liga || i.Divisao == liga.Divisao))
            .ToList();

        if (itens.Count == 0)
            return Impedida(liga, encerrada, $"A premiação {premiacao.Nome} não tem valores para {LigaLabels.Competicao(liga.Tipo, liga.Divisao)}.");

        var linhas = liga.Tipo == TipoCompetition.Liga
            ? await LinhasDaLigaAsync(liga, itens, ct)
            : await LinhasDoMataMataAsync(liga, itens, ct);

        return new PremiacaoPreviaDto(
            liga.LigaId, liga.Nome, liga.Temporada, liga.Tipo, liga.Divisao,
            encerrada, false, null, premiacao.PremiacaoId,
            linhas.Count == 0 ? "Ainda não dá para saber quem recebe o quê." : null,
            linhas);
    }

    public async Task<PremiacaoPreviaDto> PagarAsync(Guid ligaId, CancellationToken ct)
    {
        var previa = await GetPreviaAsync(ligaId, ct)
            ?? throw new InvalidOperationException("Competição não encontrada.");

        if (previa.JaPago)
            throw new InvalidOperationException("A premiação dessa competição já foi paga.");
        if (!previa.Encerrada)
            throw new InvalidOperationException("A competição precisa estar encerrada para pagar a premiação.");
        if (previa.Impedimento is not null)
            throw new InvalidOperationException(previa.Impedimento);
        if (previa.Linhas.Count == 0)
            throw new InvalidOperationException("Nenhum time a premiar nessa competição.");

        var agora = _time.GetUtcNow().UtcDateTime;
        var timeIds = previa.Linhas.Select(l => l.TimeId).ToList();
        var times = await _db.Teams.Where(t => timeIds.Contains(t.TeamId)).ToListAsync(ct);

        foreach (var linha in previa.Linhas)
        {
            var time = times.FirstOrDefault(t => t.TeamId == linha.TimeId)
                ?? throw new InvalidOperationException($"Time {linha.TimeNome} não encontrado.");

            time.Budget = decimal.Round(time.Budget + linha.Valor, 2, MidpointRounding.AwayFromZero);

            _db.PremiacaoPagamentos.Add(new PremiacaoPagamento
            {
                PagamentoId = Guid.NewGuid(),
                PremiacaoId = previa.PremiacaoId!.Value,
                LigaId = ligaId,
                TimeId = linha.TimeId,
                Valor = linha.Valor,
                Motivo = linha.Motivo,
                PagoEm = agora
            });

            // Mesmo extrato dos outros créditos de caixa.
            _db.BudgetLedgers.Add(new BudgetLedger
            {
                BudgetLedgerId = Guid.NewGuid(),
                TeamId = linha.TimeId,
                DataUtc = agora,
                Tipo = "CREDIT",
                Origem = OrigemLedger,
                Valor = linha.Valor,
                Descricao = $"{previa.LigaNome} — {linha.Motivo}"
            });
        }

        await _db.SaveChangesAsync(ct);

        return (await GetPreviaAsync(ligaId, ct))!;
    }

    public async Task EstornarAsync(Guid ligaId, CancellationToken ct)
    {
        var pagamentos = await _db.PremiacaoPagamentos.Where(p => p.LigaId == ligaId).ToListAsync(ct);
        if (pagamentos.Count == 0)
            throw new InvalidOperationException("Essa competição não tem premiação paga.");

        var liga = await _db.Ligas.AsNoTracking().FirstOrDefaultAsync(l => l.LigaId == ligaId, ct);
        var timeIds = pagamentos.Select(p => p.TimeId).ToList();
        var times = await _db.Teams.Where(t => timeIds.Contains(t.TeamId)).ToListAsync(ct);
        var agora = _time.GetUtcNow().UtcDateTime;

        foreach (var pagamento in pagamentos)
        {
            var time = times.FirstOrDefault(t => t.TeamId == pagamento.TimeId);
            if (time is null) continue;

            time.Budget = decimal.Round(Math.Max(time.Budget - pagamento.Valor, 0m), 2, MidpointRounding.AwayFromZero);

            _db.BudgetLedgers.Add(new BudgetLedger
            {
                BudgetLedgerId = Guid.NewGuid(),
                TeamId = pagamento.TimeId,
                DataUtc = agora,
                Tipo = "DEBIT",
                Origem = OrigemLedger,
                Valor = pagamento.Valor,
                Descricao = $"Estorno — {liga?.Nome ?? "competição"} — {pagamento.Motivo}"
            });
        }

        _db.PremiacaoPagamentos.RemoveRange(pagamentos);
        await _db.SaveChangesAsync(ct);
    }

    // ── Quem recebe o quê ──────────────────────────────────────────────────

    /// <summary>Liga: cada time recebe pela posição final.</summary>
    private async Task<IReadOnlyList<PremiacaoLinhaDto>> LinhasDaLigaAsync(Liga liga, List<PremiacaoItem> itens, CancellationToken ct)
    {
        var classificacao = await _db.LigaClassificacoes.AsNoTracking()
            .Where(c => c.LigaId == liga.LigaId)
            .OrderBy(c => c.Posicao)
            .Select(c => new { c.TimeId, c.Time.TeamName, c.Posicao })
            .ToListAsync(ct);

        var linhas = new List<PremiacaoLinhaDto>();
        foreach (var time in classificacao)
        {
            var item = itens.FirstOrDefault(i =>
                time.Posicao >= (i.PosicaoDe ?? 0) && time.Posicao <= (i.PosicaoAte ?? i.PosicaoDe ?? 0));

            if (item is null || item.Valor <= 0) continue;

            linhas.Add(new PremiacaoLinhaDto(time.TimeId, time.TeamName, $"{time.Posicao}º lugar", item.Valor));
        }

        return linhas;
    }

    /// <summary>Copa e Supercopa: cada time recebe pela fase em que parou.</summary>
    private async Task<IReadOnlyList<PremiacaoLinhaDto>> LinhasDoMataMataAsync(Liga liga, List<PremiacaoItem> itens, CancellationToken ct)
    {
        var fases = await FasesPorTimeAsync(liga, ct);
        var ids = fases.Keys.ToList();

        var nomes = await _db.Teams.AsNoTracking()
            .Where(t => ids.Contains(t.TeamId))
            .ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);

        var linhas = new List<PremiacaoLinhaDto>();
        foreach (var (timeId, fase) in fases)
        {
            var item = itens.FirstOrDefault(i => i.Fase == fase);
            if (item is null || item.Valor <= 0) continue;

            linhas.Add(new PremiacaoLinhaDto(timeId, nomes.GetValueOrDefault(timeId, "Time"), PremiacaoLabels.Fase(fase), item.Valor));
        }

        return linhas.OrderByDescending(l => l.Valor).ThenBy(l => l.TimeNome).ToArray();
    }

    private async Task<Dictionary<Guid, FasePremiacao>> FasesPorTimeAsync(Liga liga, CancellationToken ct)
    {
        var jogos = await _db.LigaKnockoutJogos.AsNoTracking()
            .Where(k => k.LigaId == liga.LigaId)
            .Select(k => new { k.Fase, k.TimeCasaId, k.TimeForaId, k.VencedorId })
            .ToListAsync(ct);

        var fases = new Dictionary<Guid, FasePremiacao>();

        void Marcar(Guid? timeId, FasePremiacao fase)
        {
            if (timeId is not Guid id || id == Guid.Empty) return;
            // A fase mais longe vale: quem perdeu a final não volta a ser "eliminado nas quartas".
            if (!fases.TryGetValue(id, out var atual) || fase < atual) fases[id] = fase;
        }

        foreach (var jogo in jogos)
        {
            var ateOndeChegou = jogo.Fase switch
            {
                FaseKnockout.Final => FasePremiacao.Vice,
                FaseKnockout.Semi1 or FaseKnockout.Semi2 => FasePremiacao.Semifinal,
                FaseKnockout.QF1 or FaseKnockout.QF2 or FaseKnockout.QF3 or FaseKnockout.QF4 => FasePremiacao.Quartas,
                _ => FasePremiacao.FaseDeGrupos
            };

            Marcar(jogo.TimeCasaId, ateOndeChegou);
            Marcar(jogo.TimeForaId, ateOndeChegou);

            if (jogo.Fase == FaseKnockout.Final && jogo.VencedorId is Guid campeaoDaFinal)
                fases[campeaoDaFinal] = FasePremiacao.Campeao;
        }

        if (liga.CampeaoTimeId is Guid campeao)
            fases[campeao] = FasePremiacao.Campeao;

        var participantes = await ParticipantesAsync(liga.LigaId, ct);

        foreach (var timeId in participantes)
        {
            if (fases.ContainsKey(timeId)) continue;

            // Sem bracket (Supercopa e afins) o outro time do jogo único é o vice;
            // com bracket, quem não apareceu nele parou na fase de grupos.
            fases[timeId] = jogos.Count == 0 ? FasePremiacao.Vice : FasePremiacao.FaseDeGrupos;
        }

        return fases;
    }

    private async Task<IReadOnlyList<Guid>> ParticipantesAsync(Guid ligaId, CancellationToken ct)
    {
        var dosGrupos = await _db.LigaGruposTimes.AsNoTracking()
            .Where(g => g.LigaId == ligaId).Select(g => g.TimeId).ToListAsync(ct);
        if (dosGrupos.Count > 0) return dosGrupos;

        var daTabela = await _db.LigaClassificacoes.AsNoTracking()
            .Where(c => c.LigaId == ligaId).Select(c => c.TimeId).ToListAsync(ct);
        if (daTabela.Count > 0) return daTabela;

        var inscritos = await _db.LigaTimes.AsNoTracking()
            .Where(t => t.LigaId == ligaId).Select(t => t.TimeId).ToListAsync(ct);
        if (inscritos.Count > 0) return inscritos;

        var dasPartidas = await _db.LigaPartidas.AsNoTracking()
            .Where(p => p.Rodada.LigaId == ligaId)
            .Select(p => new { p.TimeCasaId, p.TimeForaId })
            .ToListAsync(ct);

        return dasPartidas.SelectMany(p => new[] { p.TimeCasaId, p.TimeForaId }).Distinct().ToArray();
    }

    private static PremiacaoPreviaDto Impedida(Liga liga, bool encerrada, string motivo) =>
        new(liga.LigaId, liga.Nome, liga.Temporada, liga.Tipo, liga.Divisao,
            encerrada, false, null, null, motivo, Array.Empty<PremiacaoLinhaDto>());

    private static PremiacaoDto ToDto(Premiacao p) =>
        new(p.PremiacaoId, p.Temporada, p.Nome, p.AtualizadoEm,
            p.Itens
                .OrderBy(i => i.Tipo).ThenBy(i => i.Divisao ?? 0)
                .ThenBy(i => i.Fase ?? 0).ThenBy(i => i.PosicaoDe ?? 0)
                .Select(i => new PremiacaoItemDto(i.PremiacaoItemId, i.Tipo, i.Divisao, i.PosicaoDe, i.PosicaoAte, i.Fase, i.Valor))
                .ToArray());
}
