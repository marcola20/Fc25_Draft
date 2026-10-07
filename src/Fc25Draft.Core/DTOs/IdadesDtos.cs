namespace Fc25Draft.Core.DTOs;

/// <summary>Quantos anos somar à idade do jogo (o save e o banco do PES estão em 2008) para chegar à idade do site.</summary>
/// <param name="TemporadaDasIdades">Temporada em que as idades do site estão.</param>
public record IdadeDaLigaDto(int TemporadaDasIdades, int AnosASomar);

/// <summary>Como estão as idades na temporada atual e o que o "envelhecer" vai fazer.</summary>
/// <param name="Temporada">Temporada mais recente com liga; nulo sem temporada.</param>
/// <param name="AplicadoEm">Quando os jogadores fizeram aniversário nesta temporada; nulo se ainda não.</param>
/// <param name="SaemDoSub23">Quem tem 23 hoje e passa a 24 (sai do draft sub-23).</param>
/// <param name="Chegam34">Quem tem 33 hoje e passa a 34 (a idade em que a aposentadoria começa a rondar).</param>
/// <param name="PodeDesfazer">Só o envelhecimento da temporada atual, e se for o último, pode ser desfeito.</param>
public record EnvelhecimentoSituacaoDto(
    int? Temporada,
    DateTime? AplicadoEm,
    int? JogadoresNoEnvelhecimento,
    int Jogadores,
    int SemIdade,
    double IdadeMedia,
    int SaemDoSub23,
    int Chegam34,
    int? UltimaTemporadaEnvelhecida,
    bool PodeDesfazer)
{
    public bool JaEnvelhecida => AplicadoEm is not null;
}
