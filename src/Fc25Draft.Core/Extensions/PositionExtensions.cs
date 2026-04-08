using Fc25Draft.Core.Enums;
using System.Collections.Generic;

namespace Fc25Draft.Core.Extensions
{
    public static class PositionExtensions
    {
        private static readonly Dictionary<string, short> PositionCodeLookup = new(StringComparer.OrdinalIgnoreCase)
        {
            ["GOL"] = (short)PositionType.Goleiro,
            ["ZAG"] = (short)PositionType.Zagueiro,
            ["LE"]  = (short)PositionType.LateralEsquerdo,
            ["LD"]  = (short)PositionType.LateralDireito,
            ["VOL"] = (short)PositionType.Volante,
            ["MLG"] = (short)PositionType.MeiaLigacao,
            ["MAT"] = (short)PositionType.MeiaAtacante,
            ["ME"]  = (short)PositionType.MeiaEsquerda,
            ["PE"]  = (short)PositionType.PontaEsquerda,
            ["MD"]  = (short)PositionType.MeiaDireita,
            ["PD"]  = (short)PositionType.PontaDireita,
            ["CA"]  = (short)PositionType.Centroavante,
            ["SA"]  = (short)PositionType.SegundoAtacante
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
                (int)PositionType.Goleiro         => "GOL",
                (int)PositionType.Zagueiro        => "ZAG",
                (int)PositionType.LateralEsquerdo => "LE",
                (int)PositionType.LateralDireito  => "LD",
                (int)PositionType.Volante         => "VOL",
                (int)PositionType.MeiaLigacao     => "MLG",
                (int)PositionType.MeiaAtacante    => "MAT",
                (int)PositionType.MeiaEsquerda    => "ME",
                (int)PositionType.PontaEsquerda   => "PE",
                (int)PositionType.MeiaDireita     => "MD",
                (int)PositionType.PontaDireita    => "PD",
                (int)PositionType.Centroavante    => "CA",
                (int)PositionType.SegundoAtacante => "SA",
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
