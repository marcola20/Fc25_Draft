using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Base de jogadores do PES 2021 embutida no sistema, para buscar e preencher atributos.</summary>
public interface IBasePesService
{
    /// <summary>
    /// Jogadores do PES cujo nome contém o texto (sem acento, sem maiúscula), ou o jogador com esse ID
    /// quando o texto é um número.
    /// </summary>
    IReadOnlyList<JogadorPesDto> Buscar(string texto, int maximo = 20);

    /// <summary>
    /// Preenche os jogadores ainda sem atributos quando há um único jogador do PES com o mesmo nome
    /// (desempatando por posição e time). Devolve quantos foram preenchidos.
    /// </summary>
    Task<int> PreencherFaltantesAsync(CancellationToken ct = default);

    /// <summary>
    /// Grava as ligações jogador do site ↔ ID do PES conferidas no Editor PES. Ligação nova ou trocada recebe os
    /// atributos da base do PES; PesId nulo só desliga (os atributos ficam). Tudo ou nada: ID do PES repetido,
    /// inexistente ou já ligado a outro jogador gera ArgumentException e nada é gravado.
    /// Com <paramref name="atualizarAtributos"/>, quem já estava ligado ao mesmo ID também recebe os atributos
    /// da base (usado depois de regerar pes-jogadores.json.gz com o save acertado).
    /// </summary>
    Task<ResultadoLigacoesPesDto> DefinirLigacoesAsync(IReadOnlyList<DefinirLigacaoPesDto> itens,
        bool atualizarAtributos = false, CancellationToken ct = default);
}
