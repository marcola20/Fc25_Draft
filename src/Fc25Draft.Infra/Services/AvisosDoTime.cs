using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Cria avisos para os times junto da operação que os gera: entram no mesmo SaveChanges (e na mesma
/// transação). Quando são gravados, <see cref="DraftDbContext.AvisosGravados"/> avisa as telas abertas.
/// </summary>
internal static class AvisosDoTime
{
    public const string Proposta = "PROPOSTA";
    public const string PropostaAceita = "PROPOSTA_ACEITA";
    public const string PropostaRecusada = "PROPOSTA_RECUSADA";
    public const string PropostaCancelada = "PROPOSTA_CANCELADA";
    public const string LanceSuperado = "LANCE_SUPERADO";
    public const string LeilaoFechando = "LEILAO_FECHANDO";
    public const string LeilaoVencido = "LEILAO_VENCIDO";
    public const string VendaPelaLista = "VENDA_LISTA";
    public const string Revenda = "REVENDA";
    public const string PropostaExpirada = "PROPOSTA_EXPIRADA";
    public const string CaixaNegativo = "CAIXA_NEGATIVO";
    public const string Escalacao = "ESCALACAO";
    public const string VezNoDraft = "VEZ_DRAFT";

    public static void Criar(DraftDbContext db, Guid teamId, string tipo, string texto, string? link, DateTime quando)
    {
        db.AvisosTimes.Add(new AvisoTime
        {
            AvisoId = Guid.NewGuid(),
            TeamId = teamId,
            Tipo = tipo,
            Texto = texto.Length > 400 ? texto[..397] + "..." : texto,
            Link = link,
            CriadoEm = quando
        });
        db.TimesComAvisoNovo.Add(teamId);
    }
}
