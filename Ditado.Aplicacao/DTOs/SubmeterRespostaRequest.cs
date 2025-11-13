namespace Ditado.Aplicacao.DTOs;

public class SubmeterRespostaRequest
{
    public List<RespostaSegmentoDto> Respostas { get; set; } = new();
}

public class RespostaSegmentoDto
{
    public int SegmentoId { get; set; }
    public string Resposta { get; set; } = string.Empty;
}