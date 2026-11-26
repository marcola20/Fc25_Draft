using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Páginas especiais do álbum (Organização, Campeões da Liga, Campeões da Copa, Lendários). O admin coloca cada
/// figurinha e, quase sempre, sobe a arte inteira dela. Entram com o próximo número do álbum, como as Lendas; com
/// dono, a figurinha não sai mais (dá para mudar texto, raridade e arte).
/// </summary>
public partial class AlbumService
{
    /// <summary>Limite da arte no servidor, o mesmo das fotos (o navegador comprime até caber; a conexão do Blazor aceita ~512 KB em base64).</summary>
    private const int TamanhoMaximoDaArte = 300 * 1024;

    public async Task<EspeciaisDoAlbumDto?> GetEspeciaisAsync(Guid albumId, CancellationToken ct)
    {
        var album = await _db.Albuns.AsNoTracking()
            .Where(a => a.AlbumId == albumId)
            .Select(a => new AlbumDto(a.AlbumId, a.Nome, a.Temporada, a.LancadoEm, a.Ativo, a.Figurinhas.Count(f => f.SaiuEm == null), a.ContaJogosAntesDoLancamento))
            .FirstOrDefaultAsync(ct);
        if (album is null) return null;

        var especiais = (await FigurinhasAsync(albumId, ct, incluirForaDoAlbum: true))
            .Where(f => f.Especial)
            .OrderBy(f => f.Pagina).ThenBy(f => f.Ordem).ThenBy(f => f.Numero)
            .ToList();
        var ids = especiais.Select(f => f.FigurinhaId).ToList();
        var donos = await _db.FigurinhasDosTreinadores.AsNoTracking()
            .Where(f => ids.Contains(f.FigurinhaId))
            .GroupBy(f => f.FigurinhaId)
            .Select(g => new { g.Key, Donos = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Donos, ct);

        var naOrganizacao = especiais.Where(f => f.Pagina == PaginaEspecial.Organizacao).ToList();
        var admins = (await _db.AdminTokens.AsNoTracking()
                .Where(t => t.IsActive)
                .Select(t => new { t.AdminTokenId, t.Description, t.TreinadorId, Pessoa = t.Treinador != null ? t.Treinador.Nome : null })
                .ToListAsync(ct))
            // A mesma pessoa com dois tokens aparece uma vez só.
            .GroupBy(t => t.TreinadorId ?? t.AdminTokenId)
            .Select(g => g.First())
            .Select(t =>
            {
                var nome = t.Pessoa ?? t.Description ?? "Admin";
                var ja = naOrganizacao.Any(f => (t.TreinadorId is not null && f.TreinadorId == t.TreinadorId)
                                                || string.Equals(f.NomeImpresso, nome, StringComparison.CurrentCultureIgnoreCase));
                return new AdminCandidatoDto(t.AdminTokenId, nome, t.TreinadorId, ja);
            })
            .OrderBy(a => a.JaNoAlbum).ThenBy(a => a.Nome, StringComparer.CurrentCulture)
            .ToList();

        var noAlbum = especiais.Where(f => f.Pagina is PaginaEspecial.CampeoesLiga or PaginaEspecial.CampeoesCopa).ToList();
        var jaNoAlbum = (await _db.Figurinhas.AsNoTracking()
                .Where(f => f.AlbumId == albumId && f.HallOfFameId != null)
                .Select(f => f.HallOfFameId!.Value)
                .ToListAsync(ct))
            .ToHashSet();
        var campeoes = (await CampeoesDoHallDaFamaAsync(ct))
            .Select(c => c with { JaNoAlbum = jaNoAlbum.Contains(c.HallOfFameId) })
            .ToList();

        return new EspeciaisDoAlbumDto(album, especiais.Select(f => new EspecialNoAlbumDto(f, donos.GetValueOrDefault(f.FigurinhaId))).ToList(),
            admins, campeoes);
    }

    public async Task<Guid> ColocarEspecialAsync(Guid albumId, NovaEspecialDto nova, string? adminToken, CancellationToken ct)
    {
        var album = await _db.Albuns.FirstOrDefaultAsync(a => a.AlbumId == albumId, ct)
            ?? throw new InvalidOperationException("Álbum não encontrado.");
        var figurinha = await NovaEspecialAsync(albumId, nova, ct);

        await RegistrarAsync(AdminActionType.FigurinhasEspeciais, adminToken, new
        {
            album = album.Nome,
            acao = "colocou",
            pagina = AlbumFigurinhas.Especial(nova.Pagina).Titulo,
            figurinha = figurinha.NomeImpresso,
            numero = figurinha.Numero
        }, ct);
        await _db.SaveChangesAsync(ct);
        return figurinha.FigurinhaId;
    }

    public async Task<int> ColocarCampeoesAsync(Guid albumId, string? adminToken, CancellationToken ct)
    {
        var album = await _db.Albuns.FirstOrDefaultAsync(a => a.AlbumId == albumId, ct)
            ?? throw new InvalidOperationException("Álbum não encontrado.");
        var jaNoAlbum = (await _db.Figurinhas.AsNoTracking()
                .Where(f => f.AlbumId == albumId && f.HallOfFameId != null)
                .Select(f => f.HallOfFameId!.Value)
                .ToListAsync(ct))
            .ToHashSet();

        var faltam = (await CampeoesDoHallDaFamaAsync(ct)).Where(c => !jaNoAlbum.Contains(c.HallOfFameId)).ToList();
        if (faltam.Count == 0)
            throw new InvalidOperationException("Todos os campeões do Hall da Fama já estão no álbum.");

        // Uma por vez: cada uma pega o número e a ordem da anterior (ainda não salvas).
        foreach (var c in faltam)
        {
            await NovaEspecialAsync(albumId, new NovaEspecialDto(c.Pagina, c.Time, c.Titulo, AlbumFigurinhas.Especial(c.Pagina).RaridadePadrao,
                c.TeamId, HallOfFameId: c.HallOfFameId), ct);
            await _db.SaveChangesAsync(ct);
        }

        await RegistrarAsync(AdminActionType.FigurinhasEspeciais, adminToken, new
        {
            album = album.Nome,
            acao = "colocou os campeões do Hall da Fama",
            figurinhas = faltam.Count,
            campeoes = string.Join(", ", faltam.Select(c => $"{c.Time} ({c.Titulo})"))
        }, ct);
        await _db.SaveChangesAsync(ct);
        return faltam.Count;
    }

    public async Task<CopiaDeEspeciaisDto> CopiarEspeciaisAsync(Guid deAlbumId, Guid paraAlbumId, PaginaEspecial? pagina, string? adminToken, CancellationToken ct)
    {
        if (deAlbumId == paraAlbumId)
            throw new InvalidOperationException("Escolha outro álbum para copiar.");

        // O contexto vive com a tela: nada de figurinha ou arte rastreada de uma ação anterior.
        _db.ChangeTracker.Clear();
        var de = await _db.Albuns.AsNoTracking().FirstOrDefaultAsync(a => a.AlbumId == deAlbumId, ct)
            ?? throw new InvalidOperationException("Álbum de origem não encontrado.");
        var para = await _db.Albuns.AsNoTracking().FirstOrDefaultAsync(a => a.AlbumId == paraAlbumId, ct)
            ?? throw new InvalidOperationException("Álbum não encontrado.");

        var origem = (await _db.Figurinhas.AsNoTracking()
                .Where(f => f.AlbumId == deAlbumId && f.Tipo == TipoFigurinha.Especial && f.Pagina != null)
                .Where(f => pagina == null || f.Pagina == pagina)
                .ToListAsync(ct))
            .OrderBy(f => f.Pagina).ThenBy(f => f.Ordem).ThenBy(f => f.Numero)
            .ToList();
        if (origem.Count == 0)
            throw new InvalidOperationException($"O {de.Nome} não tem figurinha especial{(pagina is { } p ? $" em “{AlbumFigurinhas.Especial(p).Titulo}”" : "")}.");

        var idsOrigem = origem.Select(f => f.FigurinhaId).ToList();
        var artes = await _db.FigurinhasArtes.AsNoTracking()
            .Where(a => idsOrigem.Contains(a.FigurinhaId))
            .ToDictionaryAsync(a => a.FigurinhaId, ct);

        var destino = await _db.Figurinhas.AsNoTracking()
            .Where(f => f.AlbumId == paraAlbumId && f.Tipo == TipoFigurinha.Especial && f.Pagina != null)
            .Select(f => new { f.FigurinhaId, f.Pagina, f.Ordem, f.HallOfFameId, f.TreinadorId, f.NomeImpresso, f.Destaque })
            .ToListAsync(ct);
        var idsDestino = destino.Select(f => f.FigurinhaId).ToList();
        var destinoComArte = (await _db.FigurinhasArtes.AsNoTracking()
                .Where(a => idsDestino.Contains(a.FigurinhaId))
                .Select(a => a.FigurinhaId)
                .ToListAsync(ct))
            .ToHashSet();

        static string Chave(string nome, string? destaque) =>
            $"{nome.Trim().ToLowerInvariant()}|{destaque?.Trim().ToLowerInvariant()}";

        var numero = await _db.Figurinhas.Where(f => f.AlbumId == paraAlbumId).MaxAsync(f => (int?)f.Numero, ct) ?? 0;
        var porPagina = destino.GroupBy(f => f.Pagina!.Value)
            .ToDictionary(g => g.Key, g => (Quantas: g.Count(), Ordem: g.Max(f => f.Ordem)));
        var agora = Agora;

        int copiadas = 0, jaEstavam = 0, artesCopiadas = 0, semLugar = 0;
        foreach (var f in origem)
        {
            var pag = f.Pagina!.Value;
            var igual = destino.FirstOrDefault(d => d.Pagina == pag
                && ((f.HallOfFameId != null && d.HallOfFameId == f.HallOfFameId)
                    || (f.TreinadorId != null && d.TreinadorId == f.TreinadorId)
                    || Chave(d.NomeImpresso, d.Destaque) == Chave(f.NomeImpresso, f.Destaque)));
            if (igual is not null)
            {
                jaEstavam++;
                if (!destinoComArte.Contains(igual.FigurinhaId) && artes.TryGetValue(f.FigurinhaId, out var arteDaOutra))
                {
                    _db.FigurinhasArtes.Add(CopiaDaArte(arteDaOutra, igual.FigurinhaId));
                    destinoComArte.Add(igual.FigurinhaId);
                    artesCopiadas++;
                }
                continue;
            }

            var (quantas, ordem) = porPagina.GetValueOrDefault(pag);
            if (quantas >= AlbumFigurinhas.MaximoPorPaginaEspecial)
            {
                semLugar++;
                continue;
            }
            porPagina[pag] = (quantas + 1, ordem + 1);

            var nova = new Figurinha
            {
                FigurinhaId = Guid.NewGuid(),
                AlbumId = paraAlbumId,
                Numero = ++numero,
                Tipo = TipoFigurinha.Especial,
                Raridade = f.Raridade,
                Pagina = pag,
                TeamId = f.TeamId,
                PlayerId = f.PlayerId,
                TreinadorId = f.TreinadorId,
                Papel = f.Papel,
                HallOfFameId = f.HallOfFameId,
                NomeImpresso = f.NomeImpresso,
                PosicaoSigla = f.PosicaoSigla,
                Overall = f.Overall,
                Destaque = f.Destaque,
                Ordem = ordem + 1,
                EntrouEm = agora
            };
            _db.Figurinhas.Add(nova);
            if (artes.TryGetValue(f.FigurinhaId, out var arte))
                _db.FigurinhasArtes.Add(CopiaDaArte(arte, nova.FigurinhaId));
            copiadas++;
        }

        if (copiadas + artesCopiadas > 0)
        {
            await RegistrarAsync(AdminActionType.FigurinhasEspeciais, adminToken, new
            {
                album = para.Nome,
                acao = $"copiou as especiais do {de.Nome}",
                pagina = pagina is { } p ? AlbumFigurinhas.Especial(p).Titulo : "todas",
                figurinhas = copiadas,
                artes = artesCopiadas
            }, ct);
            await _db.SaveChangesAsync(ct);
        }

        return new CopiaDeEspeciaisDto(copiadas, jaEstavam, artesCopiadas, semLugar);

        FigurinhaArte CopiaDaArte(FigurinhaArte a, Guid figurinhaId) => new()
        {
            FigurinhaId = figurinhaId,
            Imagem = a.Imagem,
            ContentType = a.ContentType,
            AtualizadaEm = agora
        };
    }

    public async Task EditarEspecialAsync(Guid figurinhaId, NovaEspecialDto dados, string? adminToken, CancellationToken ct)
    {
        var f = await _db.Figurinhas.Include(x => x.Album)
            .FirstOrDefaultAsync(x => x.FigurinhaId == figurinhaId && x.Tipo == TipoFigurinha.Especial, ct)
            ?? throw new InvalidOperationException("Figurinha especial não encontrada.");

        var antes = new { f.NomeImpresso, f.Destaque, f.Raridade };
        await PreencherAsync(f, dados, ct);

        await RegistrarAsync(AdminActionType.FigurinhasEspeciais, adminToken, new
        {
            album = f.Album.Nome,
            acao = "mudou",
            numero = f.Numero,
            antes,
            depois = new { f.NomeImpresso, f.Destaque, f.Raridade }
        }, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MoverEspecialAsync(Guid figurinhaId, bool paraCima, CancellationToken ct)
    {
        var f = await _db.Figurinhas.FirstOrDefaultAsync(x => x.FigurinhaId == figurinhaId && x.Tipo == TipoFigurinha.Especial, ct)
            ?? throw new InvalidOperationException("Figurinha especial não encontrada.");
        var daPagina = await _db.Figurinhas
            .Where(x => x.AlbumId == f.AlbumId && x.Tipo == TipoFigurinha.Especial && x.Pagina == f.Pagina)
            .OrderBy(x => x.Ordem).ThenBy(x => x.Numero)
            .ToListAsync(ct);

        var i = daPagina.FindIndex(x => x.FigurinhaId == figurinhaId);
        var j = paraCima ? i - 1 : i + 1;
        if (j < 0 || j >= daPagina.Count) return;

        (daPagina[i], daPagina[j]) = (daPagina[j], daPagina[i]);
        for (var k = 0; k < daPagina.Count; k++)
            daPagina[k].Ordem = k + 1;
        await _db.SaveChangesAsync(ct);
    }

    public async Task TirarEspecialAsync(Guid figurinhaId, string? adminToken, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transacao = await _db.Database.BeginTransactionAsync(ct);

            var f = await _db.Figurinhas.Include(x => x.Album)
                .FirstOrDefaultAsync(x => x.FigurinhaId == figurinhaId && x.Tipo == TipoFigurinha.Especial, ct)
                ?? throw new InvalidOperationException("Figurinha especial não encontrada.");
            if (await _db.FigurinhasDosTreinadores.AnyAsync(x => x.FigurinhaId == figurinhaId, ct))
                throw new InvalidOperationException($"{f.NomeImpresso} já saiu em pacote e fica no álbum.");

            // As que vêm depois descem um número, uma de cada vez por causa do número único.
            var depois = await _db.Figurinhas
                .Where(x => x.AlbumId == f.AlbumId && x.Numero > f.Numero)
                .OrderBy(x => x.Numero)
                .ToListAsync(ct);

            _db.Figurinhas.Remove(f);
            await RegistrarAsync(AdminActionType.FigurinhasEspeciais, adminToken, new
            {
                album = f.Album.Nome,
                acao = "tirou",
                pagina = f.Pagina is { } p ? AlbumFigurinhas.Especial(p).Titulo : null,
                figurinha = f.NomeImpresso
            }, ct);
            await _db.SaveChangesAsync(ct);
            foreach (var x in depois)
            {
                x.Numero--;
                await _db.SaveChangesAsync(ct);
            }

            await transacao.CommitAsync(ct);
        });
    }

    public async Task SalvarArteAsync(Guid figurinhaId, byte[] imagem, string? adminToken, CancellationToken ct)
    {
        if (imagem.Length == 0)
            throw new InvalidOperationException("Arquivo vazio.");
        if (imagem.Length > TamanhoMaximoDaArte)
            throw new InvalidOperationException($"A arte passou de {TamanhoMaximoDaArte / 1024} KB.");
        var tipo = TipoDeImagem.Detectar(imagem)
            ?? throw new InvalidOperationException("Use WebP, PNG ou JPEG.");

        var f = await _db.Figurinhas.Include(x => x.Album)
            .FirstOrDefaultAsync(x => x.FigurinhaId == figurinhaId && x.Tipo == TipoFigurinha.Especial, ct)
            ?? throw new InvalidOperationException("Figurinha especial não encontrada.");

        var arte = await _db.FigurinhasArtes.FirstOrDefaultAsync(a => a.FigurinhaId == figurinhaId, ct);
        if (arte is null)
        {
            arte = new FigurinhaArte { FigurinhaId = figurinhaId };
            _db.FigurinhasArtes.Add(arte);
        }

        arte.Imagem = imagem;
        arte.ContentType = tipo;
        arte.AtualizadaEm = Agora;

        await RegistrarAsync(AdminActionType.FigurinhasEspeciais, adminToken, new
        {
            album = f.Album.Nome,
            acao = "subiu a arte",
            numero = f.Numero,
            figurinha = f.NomeImpresso,
            kb = imagem.Length / 1024
        }, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ApagarArteAsync(Guid figurinhaId, string? adminToken, CancellationToken ct)
    {
        var arte = await _db.FigurinhasArtes.Include(a => a.Figurinha).ThenInclude(f => f.Album)
            .FirstOrDefaultAsync(a => a.FigurinhaId == figurinhaId, ct);
        if (arte is null) return;

        _db.FigurinhasArtes.Remove(arte);
        await RegistrarAsync(AdminActionType.FigurinhasEspeciais, adminToken, new
        {
            album = arte.Figurinha.Album.Nome,
            acao = "apagou a arte",
            numero = arte.Figurinha.Numero,
            figurinha = arte.Figurinha.NomeImpresso
        }, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<FigurinhaArte?> ObterArteAsync(Guid figurinhaId, CancellationToken ct) =>
        await _db.FigurinhasArtes.AsNoTracking().FirstOrDefaultAsync(a => a.FigurinhaId == figurinhaId, ct);

    // ---------------------------------------------------------------- Apoio

    /// <summary>Cria a figurinha (sem salvar) no fim da página especial, com o próximo número do álbum.</summary>
    private async Task<Figurinha> NovaEspecialAsync(Guid albumId, NovaEspecialDto nova, CancellationToken ct)
    {
        if (!Enum.IsDefined(nova.Pagina))
            throw new InvalidOperationException("Página especial desconhecida.");

        var daPagina = await _db.Figurinhas
            .Where(f => f.AlbumId == albumId && f.Tipo == TipoFigurinha.Especial && f.Pagina == nova.Pagina)
            .Select(f => new { f.Ordem, f.TreinadorId, f.HallOfFameId })
            .ToListAsync(ct);
        var info = AlbumFigurinhas.Especial(nova.Pagina);
        if (daPagina.Count >= AlbumFigurinhas.MaximoPorPaginaEspecial)
            throw new InvalidOperationException($"A página “{info.Titulo}” já tem {AlbumFigurinhas.MaximoPorPaginaEspecial} figurinhas.");
        if (nova.TreinadorId is { } pessoa && daPagina.Any(f => f.TreinadorId == pessoa))
            throw new InvalidOperationException("Essa pessoa já tem figurinha nesta página.");
        if (nova.HallOfFameId is { } titulo && await _db.Figurinhas.AnyAsync(f => f.AlbumId == albumId && f.HallOfFameId == titulo, ct))
            throw new InvalidOperationException("Esse título do Hall da Fama já está no álbum.");

        var numero = (await _db.Figurinhas.Where(f => f.AlbumId == albumId).MaxAsync(f => (int?)f.Numero, ct) ?? 0) + 1;
        var figurinha = new Figurinha
        {
            FigurinhaId = Guid.NewGuid(),
            AlbumId = albumId,
            Numero = numero,
            Tipo = TipoFigurinha.Especial,
            Pagina = nova.Pagina,
            PosicaoSigla = info.Sigla,
            Ordem = daPagina.Count == 0 ? 1 : daPagina.Max(f => f.Ordem) + 1,
            HallOfFameId = nova.HallOfFameId,
            TreinadorId = nova.TreinadorId,
            EntrouEm = Agora
        };
        await PreencherAsync(figurinha, nova, ct);
        _db.Figurinhas.Add(figurinha);
        return figurinha;
    }

    /// <summary>O que pode mudar depois: texto, raridade, clube e jogador (a página e a pessoa ficam).</summary>
    private async Task PreencherAsync(Figurinha f, NovaEspecialDto dados, CancellationToken ct)
    {
        var nome = dados.Nome?.Trim();
        if (string.IsNullOrEmpty(nome))
            throw new InvalidOperationException("Escreva o nome que vai impresso na figurinha.");
        if (nome.Length > AlbumFigurinhas.TamanhoNomeEspecial)
            throw new InvalidOperationException($"O nome passa de {AlbumFigurinhas.TamanhoNomeEspecial} letras.");
        if (!Enum.IsDefined(dados.Raridade))
            throw new InvalidOperationException("Raridade desconhecida.");
        if (dados.TeamId is { } time && !await _db.Teams.AnyAsync(t => t.TeamId == time, ct))
            throw new InvalidOperationException("Clube não encontrado.");
        if (dados.PlayerId is { } jogador && !await _db.Players.AnyAsync(p => p.PlayerId == jogador, ct))
            throw new InvalidOperationException("Jogador não encontrado.");

        f.NomeImpresso = nome;
        f.Destaque = AlbumFigurinhas.Limpar(dados.Destaque);
        f.Raridade = dados.Raridade;
        f.TeamId = dados.TeamId;
        f.PlayerId = dados.PlayerId;
    }

    /// <summary>
    /// Os títulos de Liga e de Copa do Hall da Fama, do mais antigo para o mais novo (a Supercopa fica de fora).
    /// O clube é procurado pelo nome; campeão de outra edição, sem clube cadastrado, fica sem escudo.
    /// </summary>
    private async Task<List<CampeaoCandidatoDto>> CampeoesDoHallDaFamaAsync(CancellationToken ct)
    {
        var titulos = await _db.HallOfFame.AsNoTracking()
            .Where(h => h.Tipo == TipoCompetition.Liga || h.Tipo == TipoCompetition.Copa)
            .ToListAsync(ct);
        var nomes = titulos.Select(t => t.TimeCampeao.Trim().ToLower()).Distinct().ToList();
        var times = (await _db.Teams.AsNoTracking()
                .Where(t => nomes.Contains(t.TeamName.ToLower()))
                .Select(t => new { t.TeamId, t.TeamName })
                .ToListAsync(ct))
            .GroupBy(t => t.TeamName.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().TeamId);

        return titulos
            .OrderBy(t => t.Ano ?? int.MaxValue)
            .ThenBy(t => int.TryParse(t.Temporada, out var n) ? n : int.MaxValue)
            .ThenBy(t => t.Divisao ?? Divisao.SerieA)
            .Select(t => new CampeaoCandidatoDto(
                t.HallOfFameId,
                t.Tipo == TipoCompetition.Copa ? PaginaEspecial.CampeoesCopa : PaginaEspecial.CampeoesLiga,
                t.TimeCampeao.Trim(),
                times.TryGetValue(t.TimeCampeao.Trim().ToLowerInvariant(), out var id) ? id : null,
                TituloDoCampeao(t),
                false))
            .ToList();
    }

    /// <summary>"Brasileirão 2026 · Série A", "Copa do Brasil 2020"; sem ano, "3ª temporada".</summary>
    private static string TituloDoCampeao(HallOfFameEntry t)
    {
        var quando = t.Ano?.ToString() ?? (int.TryParse(t.Temporada, out var n) ? $"{n}ª temporada" : t.Temporada?.Trim());
        var texto = string.Join(" ", new[] { t.Descricao.Trim(), quando }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (t.Divisao is { } divisao) texto += $" · {LigaLabels.Competicao(t.Tipo, divisao)}";
        return texto.Length > AlbumFigurinhas.TamanhoDestaque ? texto[..AlbumFigurinhas.TamanhoDestaque] : texto;
    }
}
