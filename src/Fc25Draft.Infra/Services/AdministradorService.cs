using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Os administradores da liga. O principal é o dono: só ele cadastra os outros, e ele
/// mesmo não pode ser desativado nem excluído, para a liga nunca ficar sem dono.
/// </summary>
public class AdministradorService : IAdministradorService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public AdministradorService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<bool> EhPrincipalAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        var limpo = token.Trim();
        return await _db.AdminTokens.AsNoTracking()
            .AnyAsync(t => t.IsPrincipal && t.IsActive && t.Token == limpo, ct);
    }

    public async Task<IReadOnlyList<AdministradorDto>> ListAsync(string tokenDoPrincipal, CancellationToken ct)
    {
        await ExigirPrincipalAsync(tokenDoPrincipal, ct);

        var admins = await _db.AdminTokens.AsNoTracking()
            .Include(t => t.Treinador)
            .ToListAsync(ct);

        return admins
            .OrderByDescending(t => t.IsPrincipal)
            .ThenByDescending(t => t.IsActive)
            .ThenBy(t => t.Description ?? "", StringComparer.CurrentCultureIgnoreCase)
            .Select(ToDto)
            .ToList();
    }

    public async Task<AdministradorDto> SalvarAsync(string tokenDoPrincipal, AdministradorSalvarRequest request, CancellationToken ct)
    {
        await ExigirPrincipalAsync(tokenDoPrincipal, ct);

        var nome = request.Nome?.Trim();
        if (string.IsNullOrWhiteSpace(nome))
            throw new InvalidOperationException("Informe o nome do administrador.");

        AdminToken admin;
        if (request.AdminTokenId is Guid id)
        {
            admin = await _db.AdminTokens.FirstOrDefaultAsync(t => t.AdminTokenId == id, ct)
                ?? throw new InvalidOperationException("Administrador não encontrado.");
        }
        else
        {
            admin = new AdminToken
            {
                AdminTokenId = Guid.NewGuid(),
                CreatedAtUtc = _time.GetUtcNow().UtcDateTime,
                IsActive = true,
            };
            _db.AdminTokens.Add(admin);
        }

        if (admin.IsPrincipal && !request.Ativo)
            throw new InvalidOperationException("O administrador principal não pode ser desativado.");

        var token = string.IsNullOrWhiteSpace(request.Token) ? await GerarTokenUnicoAsync(nome, ct) : request.Token.Trim();
        if (token != admin.Token)
            await ExigirTokenLivreAsync(token, admin.AdminTokenId, ct);

        if (request.TreinadorId is Guid treinadorId
            && !await _db.Treinadores.AnyAsync(t => t.TreinadorId == treinadorId, ct))
            throw new InvalidOperationException("Treinador não encontrado.");

        admin.Description = nome;
        admin.Token = token;
        admin.TreinadorId = request.TreinadorId;

        if (admin.IsActive != request.Ativo)
        {
            admin.IsActive = request.Ativo;
            admin.DeactivatedAtUtc = request.Ativo ? null : _time.GetUtcNow().UtcDateTime;
        }

        await _db.SaveChangesAsync(ct);
        return await GetDtoAsync(admin.AdminTokenId, ct);
    }

    public async Task<AdministradorDto> RegerarTokenAsync(string tokenDoPrincipal, Guid adminTokenId, CancellationToken ct)
    {
        await ExigirPrincipalAsync(tokenDoPrincipal, ct);

        var admin = await _db.AdminTokens.FirstOrDefaultAsync(t => t.AdminTokenId == adminTokenId, ct)
            ?? throw new InvalidOperationException("Administrador não encontrado.");

        admin.Token = await GerarTokenUnicoAsync(admin.Description ?? "", ct);
        await _db.SaveChangesAsync(ct);

        return await GetDtoAsync(admin.AdminTokenId, ct);
    }

    public async Task ExcluirAsync(string tokenDoPrincipal, Guid adminTokenId, CancellationToken ct)
    {
        await ExigirPrincipalAsync(tokenDoPrincipal, ct);

        var admin = await _db.AdminTokens.FirstOrDefaultAsync(t => t.AdminTokenId == adminTokenId, ct)
            ?? throw new InvalidOperationException("Administrador não encontrado.");

        if (admin.IsPrincipal)
            throw new InvalidOperationException("O administrador principal não pode ser excluído.");

        _db.AdminTokens.Remove(admin);
        await _db.SaveChangesAsync(ct);
    }

    private async Task ExigirPrincipalAsync(string? token, CancellationToken ct)
    {
        if (!await EhPrincipalAsync(token, ct))
            throw new AdminForbiddenException("Só o administrador principal pode gerenciar os administradores.");
    }

    /// <summary>
    /// O token de admin é conferido antes do de treinador na hora de entrar, então ele não
    /// pode bater com o de ninguém — senão um treinador viraria administrador sem querer.
    /// </summary>
    private async Task ExigirTokenLivreAsync(string token, Guid adminTokenId, CancellationToken ct)
    {
        var maiusculo = token.ToUpper();

        var deOutroAdmin = await _db.AdminTokens
            .AnyAsync(t => t.AdminTokenId != adminTokenId && t.Token.ToUpper() == maiusculo, ct);
        var deTreinador = await _db.Treinadores
            .AnyAsync(t => t.Token.ToUpper() == maiusculo, ct);

        if (deOutroAdmin || deTreinador)
            throw new InvalidOperationException("Esse token já é usado por outra pessoa.");
    }

    private async Task<string> GerarTokenUnicoAsync(string nome, CancellationToken ct)
    {
        var limpo = new string(nome.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).Take(10).ToArray());
        var prefixo = limpo.Length > 0 ? $"ADM-{limpo}" : "ADM";

        while (true)
        {
            var token = $"{prefixo}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
            var usado = await _db.AdminTokens.AnyAsync(t => t.Token.ToUpper() == token, ct)
                     || await _db.Treinadores.AnyAsync(t => t.Token.ToUpper() == token, ct);
            if (!usado) return token;
        }
    }

    private async Task<AdministradorDto> GetDtoAsync(Guid adminTokenId, CancellationToken ct)
    {
        var admin = await _db.AdminTokens.AsNoTracking()
            .Include(t => t.Treinador)
            .FirstAsync(t => t.AdminTokenId == adminTokenId, ct);

        return ToDto(admin);
    }

    private static AdministradorDto ToDto(AdminToken t) =>
        new(t.AdminTokenId,
            string.IsNullOrWhiteSpace(t.Description) ? "(sem nome)" : t.Description!,
            t.Token,
            t.IsActive,
            t.IsPrincipal,
            t.TreinadorId,
            t.Treinador?.Nome,
            t.CreatedAtUtc,
            t.LastUsedAtUtc);
}
