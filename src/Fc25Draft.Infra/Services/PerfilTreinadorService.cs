using System.Text.Json;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Perfil do treinador. A pessoa edita livremente; o admin apaga o que passar do ponto (com registro no
/// Log de Ações). As consultas de lista nunca trazem a imagem, só se ela existe e a versão.
/// </summary>
public class PerfilTreinadorService : IPerfilTreinadorService
{
    private const int TamanhoMaximoFoto = 300 * 1024;

    private static readonly JsonSerializerOptions JsonLog = new(JsonSerializerDefaults.Web);

    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public PerfilTreinadorService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    private DateTime Agora => _time.GetUtcNow().UtcDateTime;

    public async Task<PerfilTreinadorDto?> GetAsync(Guid treinadorId, CancellationToken ct) =>
        (await VariosAsync(new[] { treinadorId }, ct)).GetValueOrDefault(treinadorId);

    public async Task<IReadOnlyDictionary<Guid, PerfilTreinadorDto>> VariosAsync(IEnumerable<Guid> treinadorIds, CancellationToken ct)
    {
        var ids = treinadorIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, PerfilTreinadorDto>();

        var lidos = await _db.Treinadores.AsNoTracking()
            .Where(t => ids.Contains(t.TreinadorId))
            .Select(t => new
            {
                t.TreinadorId,
                t.Nome,
                Perfil = _db.PerfisTreinadores
                    .Where(p => p.TreinadorId == t.TreinadorId)
                    .Select(p => new { p.Apelido, p.Frase, p.Esquema, p.FotoAtualizadaEm, TemFoto = p.Imagem != null })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return lidos.ToDictionary(l => l.TreinadorId, l => l.Perfil is null
            ? PerfilTreinadorDto.SoNome(l.TreinadorId, l.Nome)
            : new PerfilTreinadorDto(l.TreinadorId, l.Nome, l.Perfil.Apelido, l.Perfil.Frase, l.Perfil.Esquema,
                l.Perfil.TemFoto, Versao(l.Perfil.TemFoto, l.Perfil.FotoAtualizadaEm)));
    }

    public async Task<IReadOnlyList<ComissaoTecnicaDto>> ComissaoTecnicaAsync(Guid teamId, CancellationToken ct)
    {
        var passagens = await _db.TreinadorPassagens.AsNoTracking()
            .Where(p => p.TimeId == teamId && p.Ate == null)
            .OrderBy(p => p.Papel).ThenBy(p => p.Desde)
            .Select(p => new { p.TreinadorId, p.Papel, p.Desde })
            .ToListAsync(ct);
        var perfis = await VariosAsync(passagens.Select(p => p.TreinadorId), ct);

        return passagens
            .Where(p => perfis.ContainsKey(p.TreinadorId))
            .Select(p => new ComissaoTecnicaDto(perfis[p.TreinadorId], p.Papel, p.Desde))
            .ToList();
    }

    public async Task<PerfilTreinadorDto> SalvarAsync(Guid treinadorId, PerfilSalvarRequest request, CancellationToken ct)
    {
        var apelido = Limpar(request.Apelido, PerfilTreinador.TamanhoApelido, "O apelido");
        var frase = Limpar(request.Frase, PerfilTreinador.TamanhoFrase, "A frase");
        string? esquema = null;
        if (!string.IsNullOrWhiteSpace(request.Esquema))
        {
            esquema = EsquemasTaticos.Achar(request.Esquema)?.Nome
                ?? throw new InvalidOperationException("Escolha um esquema da lista.");
        }

        var perfil = await PerfilParaEditarAsync(treinadorId, ct);
        perfil.Apelido = apelido;
        perfil.Frase = frase;
        perfil.Esquema = esquema;
        perfil.AtualizadoEm = Agora;
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(treinadorId, ct))!;
    }

    public async Task<PerfilTreinadorDto> TrocarFotoAsync(Guid treinadorId, byte[] imagem, CancellationToken ct)
    {
        if (imagem.Length == 0 || imagem.Length > TamanhoMaximoFoto)
            throw new InvalidOperationException("A foto precisa ter até 300 KB.");
        var tipo = TipoDeImagem.Detectar(imagem) ?? throw new InvalidOperationException("Use uma imagem WebP, PNG ou JPEG.");

        var perfil = await PerfilParaEditarAsync(treinadorId, ct);
        perfil.Imagem = imagem;
        perfil.ContentType = tipo;
        perfil.FotoAtualizadaEm = Agora;
        perfil.AtualizadoEm = Agora;
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(treinadorId, ct))!;
    }

    public async Task<PerfilTreinadorDto> RemoverFotoAsync(Guid treinadorId, CancellationToken ct)
    {
        await TirarFotoAsync(treinadorId, ct);
        return (await GetAsync(treinadorId, ct)) ?? throw new InvalidOperationException("Pessoa não encontrada.");
    }

    public async Task<FotoTreinadorArquivo?> ObterFotoAsync(Guid treinadorId, CancellationToken ct) =>
        await _db.PerfisTreinadores.AsNoTracking()
            .Where(p => p.TreinadorId == treinadorId && p.Imagem != null)
            .Select(p => new FotoTreinadorArquivo(p.Imagem!, p.ContentType!, p.FotoAtualizadaEm ?? p.AtualizadoEm))
            .FirstOrDefaultAsync(ct);

    public async Task<PerfilTreinadorDto> ApagarPeloAdminAsync(Guid treinadorId, CampoDoPerfil campo, string? adminToken, CancellationToken ct)
    {
        var perfil = await _db.PerfisTreinadores.FirstOrDefaultAsync(p => p.TreinadorId == treinadorId, ct);
        var nome = await _db.Treinadores.Where(t => t.TreinadorId == treinadorId).Select(t => t.Nome).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Pessoa não encontrada.");

        string? apagado;
        switch (campo)
        {
            case CampoDoPerfil.Foto:
                apagado = perfil?.Imagem is null ? null : "foto";
                if (perfil is not null) { perfil.Imagem = null; perfil.ContentType = null; perfil.FotoAtualizadaEm = Agora; }
                break;
            case CampoDoPerfil.Apelido:
                apagado = perfil?.Apelido;
                if (perfil is not null) perfil.Apelido = null;
                break;
            case CampoDoPerfil.Frase:
                apagado = perfil?.Frase;
                if (perfil is not null) perfil.Frase = null;
                break;
            default:
                throw new InvalidOperationException("Escolha o que apagar.");
        }

        if (apagado is null) throw new InvalidOperationException("Não tem nada para apagar.");
        perfil!.AtualizadoEm = Agora;

        _db.AdminActionsLogs.Add(new AdminActionsLog
        {
            ActionId = Guid.NewGuid(),
            ActionType = AdminActionType.ApagarPerfilTreinador,
            PerformedBy = await AdminIdAsync(adminToken, ct),
            PayloadJson = JsonSerializer.Serialize(new
            {
                treinador = nome,
                campo = campo switch { CampoDoPerfil.Foto => "Foto", CampoDoPerfil.Apelido => "Apelido", _ => "Frase" },
                // O texto apagado fica no log, para a organização saber o que foi; a foto não.
                apagado = campo == CampoDoPerfil.Foto ? null : apagado
            }, JsonLog),
            CreatedAtUtc = Agora
        });
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(treinadorId, ct))!;
    }

    // ---------------------------------------------------------------- Apoio

    private async Task TirarFotoAsync(Guid treinadorId, CancellationToken ct)
    {
        var perfil = await _db.PerfisTreinadores.FirstOrDefaultAsync(p => p.TreinadorId == treinadorId, ct);
        if (perfil?.Imagem is null) return;
        perfil.Imagem = null;
        perfil.ContentType = null;
        perfil.FotoAtualizadaEm = Agora;
        perfil.AtualizadoEm = Agora;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>O perfil para editar; na primeira vez, cria (só para quem existe).</summary>
    private async Task<PerfilTreinador> PerfilParaEditarAsync(Guid treinadorId, CancellationToken ct)
    {
        var perfil = await _db.PerfisTreinadores.FirstOrDefaultAsync(p => p.TreinadorId == treinadorId, ct);
        if (perfil is not null) return perfil;

        if (!await _db.Treinadores.AnyAsync(t => t.TreinadorId == treinadorId, ct))
            throw new InvalidOperationException("Pessoa não encontrada.");
        perfil = new PerfilTreinador { TreinadorId = treinadorId, AtualizadoEm = Agora };
        _db.PerfisTreinadores.Add(perfil);
        return perfil;
    }

    private static string? Limpar(string? texto, int tamanho, string oQue)
    {
        var limpo = string.Join(' ', (texto ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (limpo.Length == 0) return null;
        if (limpo.Length > tamanho) throw new InvalidOperationException($"{oQue} pode ter até {tamanho} letras.");
        return limpo;
    }

    /// <summary>Muda a cada troca de foto (vai no endereço da imagem, para o navegador não mostrar a antiga).</summary>
    internal static string Versao(bool temFoto, DateTime? em) => temFoto && em is { } quando ? quando.Ticks.ToString("x") : "0";

    private async Task<string> AdminIdAsync(string? adminToken, CancellationToken ct)
    {
        var limpo = adminToken?.Trim();
        var id = string.IsNullOrEmpty(limpo)
            ? null
            : await _db.AdminTokens.AsNoTracking().Where(t => t.Token == limpo).Select(t => (Guid?)t.AdminTokenId).FirstOrDefaultAsync(ct);
        return id?.ToString() ?? "admin";
    }
}
