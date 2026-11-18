using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// O +1 over de prêmio (destaque votado ou estatística): sobe os atributos até a fórmula do PES dar +1, como a
/// venda rápida, e deixa a mudança pendente para o Editor PES levar ao save.
/// </summary>
internal static class OverDePremio
{
    /// <summary>Tamanho da coluna EvolucoesPes.Motivo.</summary>
    private const int MotivoMaximo = 60;

    /// <summary>
    /// Sobe o jogador (carregado com <c>Atributos</c>). Lança antes de mexer em qualquer coisa quando não dá.
    /// Devolve o registro para o Editor PES, já no contexto e sem salvar, ou nulo se o jogador não tem atributos
    /// do jogo (aí só o overall do site sobe).
    /// </summary>
    public static (EvolucaoPes? Registro, string Resumo) Dar(DraftDbContext db, Player jogador, string motivo, DateTime agoraUtc)
    {
        var antesOverall = jogador.Overall;

        if (jogador.Atributos?.PosicaoPes is not int pos)
        {
            jogador.Overall = Math.Min(AtributosPes.Maximo, jogador.Overall + 1);
            return (null, $"{jogador.Name}: overall {antesOverall} → {jogador.Overall} só no site (sem atributos do jogo — dê o over no PES à mão).");
        }

        var dto = AtributosPes.ParaDto(jogador.Atributos);
        var antes = OverallPes.Valores(dto);
        var evolucao = OverallPes.Evoluir(antes, pos, dto.EstiloDeJogo, dto.PeFracoUso, dto.PeFracoPrecisao, antesOverall + 1);
        if (evolucao.Novos.SequenceEqual(antes))
            throw new InvalidOperationException($"{jogador.Name}: os atributos já estão no limite, nada mudou.");

        for (var i = 0; i < antes.Length; i++)
            AtributosPes.Todos[i].Set(dto, evolucao.Novos[i]);
        AtributosPes.Aplicar(dto, jogador.Atributos);
        OverallPes.Recalcular(jogador);

        var registro = new EvolucaoPes
        {
            PlayerId = jogador.PlayerId,
            Motivo = motivo.Length > MotivoMaximo ? motivo[..(MotivoMaximo - 1)] + "…" : motivo,
            CriadaEmUtc = agoraUtc,
            OverallAntes = antesOverall,
            OverallDepois = jogador.Overall,
            Mudancas = EvolucaoPes.EscreverMudancas(antes, evolucao.Novos),
        };
        db.EvolucoesPes.Add(registro);

        return (registro, $"{jogador.Name}: overall {antesOverall} → {jogador.Overall} · {OverallPes.DescreverMudancas(antes, evolucao.Novos)}");
    }

    /// <summary>
    /// Desfaz o over (jogador carregado com <c>Atributos</c>). Lança se o Editor PES já levou a mudança ao save.
    /// Remove o registro do contexto, sem salvar.
    /// </summary>
    public static void Desfazer(DraftDbContext db, Player jogador, EvolucaoPes? registro)
    {
        if (registro is null)
        {
            jogador.Overall = Math.Max(1, jogador.Overall - 1);
            return;
        }

        if (registro.AplicadaNoJogoEmUtc is not null)
            throw new InvalidOperationException($"O Editor PES já levou o over de {jogador.Name} para o save: desfaça no jogo e ajuste na ficha.");

        var dto = AtributosPes.ParaDto(jogador.Atributos!);
        var valores = OverallPes.Valores(dto);
        var mudancas = registro.LerMudancas();
        for (var i = 0; i < valores.Length; i++)
            AtributosPes.Todos[i].Set(dto, Math.Clamp(valores[i] - mudancas[i], AtributosPes.Minimo, AtributosPes.Maximo));
        AtributosPes.Aplicar(dto, jogador.Atributos!);
        OverallPes.Recalcular(jogador);
        db.EvolucoesPes.Remove(registro);
    }
}
