using Fc25Draft.Core.Enums;
using System.Collections.Generic;

namespace Fc25Draft.Core.Extensions
{
    public static class PositionExtensions
    {
        // Vocabulário único de código de posição (estilo internacional GK/CB/LB…).
        private static readonly Dictionary<string, short> PositionCodeLookup = new(StringComparer.OrdinalIgnoreCase)
        {
            ["GK"]  = (short)PositionType.Goleiro,
            ["CB"]  = (short)PositionType.Zagueiro,
            ["LB"]  = (short)PositionType.LateralEsquerdo,
            ["RB"]  = (short)PositionType.LateralDireito,
            ["CDM"] = (short)PositionType.Volante,
            ["CM"]  = (short)PositionType.MeiaLigacao,
            ["CAM"] = (short)PositionType.MeiaAtacante,
            ["LM"]  = (short)PositionType.MeiaEsquerda,
            ["LW"]  = (short)PositionType.PontaEsquerda,
            ["RM"]  = (short)PositionType.MeiaDireita,
            ["RW"]  = (short)PositionType.PontaDireita,
            ["ST"]  = (short)PositionType.Centroavante,
            ["CF"]  = (short)PositionType.SegundoAtacante
        };

        public static int ToPositionId(this string positionName)
        {
            if (string.IsNullOrWhiteSpace(positionName))
                return 0;

            return positionName.Trim().ToLowerInvariant() switch
            {
                "goleiro"           => (int)PositionType.Goleiro,
                "zagueiro"          => (int)PositionType.Zagueiro,
                "lateral esquerdo"  => (int)PositionType.LateralEsquerdo,
                "lateral direito"   => (int)PositionType.LateralDireito,
                "volante"           => (int)PositionType.Volante,
                "meia de ligação"
                or "meia de ligacao" => (int)PositionType.MeiaLigacao,
                "meia atacante"     => (int)PositionType.MeiaAtacante,
                "meia esquerda"     => (int)PositionType.MeiaEsquerda,
                "ponta esquerda"    => (int)PositionType.PontaEsquerda,
                "meia direita"      => (int)PositionType.MeiaDireita,
                "ponta direita"     => (int)PositionType.PontaDireita,
                "centroavante"      => (int)PositionType.Centroavante,
                "segundo atacante"  => (int)PositionType.SegundoAtacante,
                _ => 0
            };
        }

        public static string ToPositionName(this int positionId)
        {
            return positionId switch
            {
                (int)PositionType.Goleiro         => "Goleiro",
                (int)PositionType.Zagueiro        => "Zagueiro",
                (int)PositionType.LateralEsquerdo => "Lateral Esquerdo",
                (int)PositionType.LateralDireito  => "Lateral Direito",
                (int)PositionType.Volante         => "Volante",
                (int)PositionType.MeiaLigacao     => "Meia de Ligação",
                (int)PositionType.MeiaAtacante    => "Meia Atacante",
                (int)PositionType.MeiaEsquerda    => "Meia Esquerda",
                (int)PositionType.PontaEsquerda   => "Ponta Esquerda",
                (int)PositionType.MeiaDireita     => "Meia Direita",
                (int)PositionType.PontaDireita    => "Ponta Direita",
                (int)PositionType.Centroavante    => "Centroavante",
                (int)PositionType.SegundoAtacante => "Segundo Atacante",
                _ => "Desconhecida"
            };
        }

        public static string ToPositionCode(this int positionId)
        {
            return positionId switch
            {
                (int)PositionType.Goleiro         => "GK",
                (int)PositionType.Zagueiro        => "CB",
                (int)PositionType.LateralEsquerdo => "LB",
                (int)PositionType.LateralDireito  => "RB",
                (int)PositionType.Volante         => "CDM",
                (int)PositionType.MeiaLigacao     => "CM",
                (int)PositionType.MeiaAtacante    => "CAM",
                (int)PositionType.MeiaEsquerda    => "LM",
                (int)PositionType.PontaEsquerda   => "LW",
                (int)PositionType.MeiaDireita     => "RM",
                (int)PositionType.PontaDireita    => "RW",
                (int)PositionType.Centroavante    => "ST",
                (int)PositionType.SegundoAtacante => "CF",
                _ => positionId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
        }

        public static bool TryParsePositionCode(string? code, out short positionId)
        {
            positionId = 0;

            if (string.IsNullOrWhiteSpace(code))
                return false;

            return PositionCodeLookup.TryGetValue(code.Trim(), out positionId);
        }
    }
}
