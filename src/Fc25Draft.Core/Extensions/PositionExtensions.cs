using Fc25Draft.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fc25Draft.Core.Extensions
{
    public static class PositionExtensions
    {
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
    }
}
