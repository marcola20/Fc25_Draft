using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.DTOs;

/// <summary>
/// O perfil de uma pessoa como as telas mostram. <see cref="NomeCurto"/> é o que vai onde o espaço é curto
/// (o apelido, se tiver); o nome completo vai na dica. <see cref="VersaoFoto"/> muda a cada troca de foto.
/// </summary>
public record PerfilTreinadorDto(
    Guid TreinadorId,
    string Nome,
    string? Apelido,
    string? Frase,
    string? Esquema,
    bool TemFoto,
    string VersaoFoto)
{
    public string NomeCurto => string.IsNullOrWhiteSpace(Apelido) ? Nome : Apelido!;

    /// <summary>Perfil de quem ainda não montou nada: só o nome.</summary>
    public static PerfilTreinadorDto SoNome(Guid treinadorId, string nome) => new(treinadorId, nome, null, null, null, false, "0");
}

/// <summary>Quem está no comando do clube hoje (passagem aberta), com o perfil.</summary>
public record ComissaoTecnicaDto(PerfilTreinadorDto Perfil, PapelTreinador Papel, DateTime Desde);

public record PerfilSalvarRequest(string? Apelido, string? Frase, string? Esquema);

/// <summary>O que o admin pode apagar do perfil de alguém.</summary>
public enum CampoDoPerfil
{
    Foto = 1,
    Apelido = 2,
    Frase = 3
}

public record FotoTreinadorArquivo(byte[] Imagem, string ContentType, DateTime AtualizadaEm);
