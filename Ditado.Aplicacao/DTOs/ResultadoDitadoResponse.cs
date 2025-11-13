namespace Ditado.Aplicacao.DTOs;

public class ResultadoDitadoResponse
{
    public int RespostaDitadoId { get; set; }
    public decimal Pontuacao { get; set; }
    public int TotalLacunas { get; set; }
    public int Acertos { get; set; }
    public int Erros { get; set; }
    public List<DetalheRespostaDto> Detalhes { get; set; } = new();
}

public class DetalheRespostaDto
{
    public int SegmentoId { get; set; }
    public string RespostaFornecida { get; set; } = string.Empty;
    public string RespostaEsperada { get; set; } = string.Empty;
    public bool Correto { get; set; }
    public string? TipoErro { get; set; }
}