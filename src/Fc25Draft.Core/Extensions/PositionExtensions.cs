using Fc25Draft.Core.Enums;
using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.Extensions
{
    public static class PositionExtensions
    {
        private static readonly Dictionary<string, short> PositionCodeLookup = new(StringComparer.OrdinalIgnoreCase)
        {
            ["GOL"] = (short)PositionType.Goleiro,
            ["ZAG"] = (short)PositionType.Zagueiro,
            ["LE"] = (short)PositionType.LateralAlaEsquerdo,
            ["LD"] = (short)PositionType.LateralAlaDireito,
            ["VOL"] = (short)PositionType.Volante,
            ["MEI"] = (short)PositionType.MeiaCentral,
            ["MAT"] = (short)PositionType.MeiaAtacante,
            ["MPE"] = (short)PositionType.MeiaPontaEsquerda,
            ["MPD"] = (short)PositionType.MeiaPontaDireita,
            ["ATA"] = (short)PositionType.Atacante
        };

        public static int ToPositionId(this string positionName)
        {
            if (string.IsNullOrWhiteSpace(positionName))
                return 0;

            positionName = positionName.Trim().ToLowerInvariant();

            return positionName switch
            {
                "goleiro" => (int)PositionType.Goleiro,
                "zagueiro" => (int)PositionType.Zagueiro,
                "lateral/ala esquerdo" => (int)PositionType.LateralAlaEsquerdo,
                "lateral/ala direito" => (int)PositionType.LateralAlaDireito,
                "volante" => (int)PositionType.Volante,
                "meia central" => (int)PositionType.MeiaCentral,
                "meia atacante" => (int)PositionType.MeiaAtacante,
                "meia/ponta esquerda" => (int)PositionType.MeiaPontaEsquerda,
                "meia/ponta direita" => (int)PositionType.MeiaPontaDireita,
                "atacante" => (int)PositionType.Atacante,
                _ => 0
            };
        }

        public static string ToPositionName(this int positionId)
        {
            return positionId switch
            {
                (int)PositionType.Goleiro => "Goleiro",
                (int)PositionType.Zagueiro => "Zagueiro",
                (int)PositionType.LateralAlaEsquerdo => "Lateral/Ala Esquerdo",
                (int)PositionType.LateralAlaDireito => "Lateral/Ala Direito",
                (int)PositionType.Volante => "Volante",
                (int)PositionType.MeiaCentral => "Meia Central",
                (int)PositionType.MeiaAtacante => "Meia Atacante",
                (int)PositionType.MeiaPontaEsquerda => "Meia/Ponta Esquerda",
                (int)PositionType.MeiaPontaDireita => "Meia/Ponta Direita",
                (int)PositionType.Atacante => "Atacante",
                _ => "Desconhecida"
            };
        }

        public static string ToPositionCode(this int positionId)
        {
            return positionId switch
            {
                (int)PositionType.Goleiro => "GOL",
                (int)PositionType.Zagueiro => "ZAG",
                (int)PositionType.LateralAlaEsquerdo => "LE",
                (int)PositionType.LateralAlaDireito => "LD",
                (int)PositionType.Volante => "VOL",
                (int)PositionType.MeiaCentral => "MEI",
                (int)PositionType.MeiaAtacante => "MAT",
                (int)PositionType.MeiaPontaEsquerda => "MPE",
                (int)PositionType.MeiaPontaDireita => "MPD",
                (int)PositionType.Atacante => "ATA",
                _ => positionId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
        }

        public static bool TryParsePositionCode(string? code, out short positionId)
        {
            positionId = 0;

            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            return PositionCodeLookup.TryGetValue(code.Trim(), out positionId);
        }
    }
}
