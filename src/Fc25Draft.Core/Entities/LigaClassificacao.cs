using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

public class LigaClassificacao
{
    public Guid ClassificacaoId { get; set; }
    public Guid LigaId { get; set; }
    public Guid TimeId { get; set; }
    public int Posicao { get; set; }
    public int Pontos { get; set; }
    public int Jogos { get; set; }
    public int Vitorias { get; set; }
    public int Empates { get; set; }
    public int Derrotas { get; set; }
    public int GolsPro { get; set; }
    public int GolsContra { get; set; }
    // Saldo de gols é sempre GolsPro - GolsContra (computado; não persiste, evita desatualizar).
    public int SaldoGols => GolsPro - GolsContra;
    public int CartoesAmarelos { get; set; }
    public int CartoesVermelhos { get; set; }
    public GrupoCopa? Grupo { get; set; }

    public Liga Liga { get; set; } = null!;
    public Team Time { get; set; } = null!;
}
