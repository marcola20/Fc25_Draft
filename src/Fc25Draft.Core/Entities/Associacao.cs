namespace Fc25Draft.Core.Entities;

/// <summary>Em que pé está a eleição da Associação de Treinadores.</summary>
public enum SituacaoEleicao
{
    /// <summary>O admin está montando; ninguém vê.</summary>
    Rascunho = 0,

    /// <summary>Edital no ar: calendário, chapas inscritas e lista de eleitores aparecem na página. Ainda não vota.</summary>
    Inscricoes = 1,

    /// <summary>Urna aberta (dentro do horário de abertura e encerramento, quando definidos).</summary>
    Votacao = 2,

    /// <summary>Urna fechada; só o admin vê a contagem antes de homologar.</summary>
    Apuracao = 3,

    /// <summary>Resultado homologado e publicado. Se uma chapa passou de 50% dos válidos, ela é a diretoria.</summary>
    Resultado = 4
}

/// <summary>Os cinco cargos de uma chapa (Art. 5º do edital).</summary>
public enum CargoAssociacao
{
    Presidente = 1,
    VicePresidente = 2,
    SecretarioGeral = 3,
    DiretorCompeticoes = 4,
    DiretorEtica = 5
}

/// <summary>Uma eleição (um turno) da Diretoria Executiva da Associação de Treinadores.</summary>
public class EleicaoAssociacao
{
    public Guid EleicaoId { get; set; }

    /// <summary>"Eleição da Diretoria 2011".</summary>
    public string Titulo { get; set; } = null!;

    /// <summary>1 ou 2. O segundo turno nasce do primeiro, com as duas chapas mais votadas.</summary>
    public int Turno { get; set; } = 1;

    public SituacaoEleicao Situacao { get; set; }

    /// <summary>Fim das inscrições de chapa (UTC). Só informativo: quem inscreve a chapa no site é o admin.</summary>
    public DateTime? InscricoesAte { get; set; }

    /// <summary>A urna só aceita voto a partir daqui (UTC), mesmo com a votação aberta.</summary>
    public DateTime? AbreEm { get; set; }

    /// <summary>A urna para de aceitar voto aqui (UTC), mesmo que o admin ainda não tenha encerrado.</summary>
    public DateTime? FechaEm { get; set; }

    /// <summary>Edital/regras da eleição, no Markdown simples do site.</summary>
    public string Edital { get; set; } = "";

    /// <summary>Quem a comissão eleitoral é (nomes), mostrado na página.</summary>
    public string Comissao { get; set; } = "";

    /// <summary>Chapa eleita (mais de 50% dos votos válidos). Nulo sem vencedor ou antes do resultado.</summary>
    public Guid? ChapaVencedoraId { get; set; }

    public DateTime CriadaEm { get; set; }
    public DateTime? VotacaoAbertaEm { get; set; }
    public DateTime? EncerradaEm { get; set; }
    public DateTime? ResultadoEm { get; set; }

    public ICollection<ChapaAssociacao> Chapas { get; set; } = new List<ChapaAssociacao>();
    public ICollection<EleicaoEleitor> Eleitores { get; set; } = new List<EleicaoEleitor>();
}

/// <summary>Uma chapa inscrita, com os cinco cargos.</summary>
public class ChapaAssociacao
{
    public Guid ChapaId { get; set; }
    public Guid EleicaoId { get; set; }

    /// <summary>Número na cédula (1, 2...).</summary>
    public int Numero { get; set; }

    public string Nome { get; set; } = null!;

    /// <summary>Plano de gestão, no Markdown simples do site.</summary>
    public string Plano { get; set; } = "";

    public EleicaoAssociacao Eleicao { get; set; } = null!;
    public ICollection<ChapaMembro> Membros { get; set; } = new List<ChapaMembro>();
}

public class ChapaMembro
{
    public Guid ChapaId { get; set; }
    public CargoAssociacao Cargo { get; set; }
    public Guid TreinadorId { get; set; }

    public ChapaAssociacao Chapa { get; set; } = null!;
    public Treinador Treinador { get; set; } = null!;
}

/// <summary>
/// Quem pode votar nesta eleição (a lista publicada antes da votação) e se já votou. Não diz em quem:
/// o voto fica em <see cref="EleicaoVoto"/>, sem ligação com a pessoa.
/// </summary>
public class EleicaoEleitor
{
    public Guid EleicaoId { get; set; }
    public Guid TreinadorId { get; set; }

    /// <summary>Nulo enquanto não votou.</summary>
    public DateTime? VotouEm { get; set; }

    public EleicaoAssociacao Eleicao { get; set; } = null!;
    public Treinador Treinador { get; set; } = null!;
}

/// <summary>
/// Um voto na urna. De propósito, sem eleitor e sem horário: não dá para saber quem votou em quem,
/// nem cruzando com a hora em que cada um votou.
/// </summary>
public class EleicaoVoto
{
    public Guid VotoId { get; set; }
    public Guid EleicaoId { get; set; }

    /// <summary>Nulo = voto em branco.</summary>
    public Guid? ChapaId { get; set; }
}
