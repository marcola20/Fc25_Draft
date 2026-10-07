namespace Fc25Draft.Core.DTOs;

/// <summary>Um jogo no resumo, já com quem marcou.</summary>
public record ResumoJogoDto(
    string Casa,
    int GolsCasa,
    int GolsFora,
    string Fora,
    IReadOnlyList<string> MarcadoresCasa,
    IReadOnlyList<string> MarcadoresFora)
{
    public int TotalDeGols => GolsCasa + GolsFora;
    public int Diferenca => Math.Abs(GolsCasa - GolsFora);
    public string Placar => $"{Casa} {GolsCasa} x {GolsFora} {Fora}";
}

public record ResumoArtilheiroDto(string Nome, string TimeNome, int Gols);

public record ResumoTabelaLinhaDto(int Posicao, string TimeNome, int Pontos, int Jogos, int SaldoGols);

/// <summary>Uma rodada fechada, pronta para virar texto de grupo.</summary>
public record ResumoDaRodadaDto(
    Guid RodadaId,
    string Titulo,
    DateTime? Quando,
    bool Completa,
    IReadOnlyList<ResumoJogoDto> Jogos,
    IReadOnlyList<ResumoArtilheiroDto> Artilheiros,
    IReadOnlyList<ResumoTabelaLinhaDto> Tabela,
    IReadOnlyList<BolaoRankingLinhaDto> Bolao,
    string? ProximaRodada,
    DiretoriaResumoRodadaDto? Diretoria = null);

/// <summary>Uma rodada na lista de escolha do resumo.</summary>
public record ResumoRodadaOpcaoDto(Guid RodadaId, string Titulo, DateTime? Quando, int Jogos, bool Completa);
