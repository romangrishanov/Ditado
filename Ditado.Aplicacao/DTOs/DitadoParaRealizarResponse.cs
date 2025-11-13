namespace Ditado.Aplicacao.DTOs;

public class DitadoParaRealizarResponse
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string AudioBase64 { get; set; } = string.Empty;
    public List<SegmentoParaRealizarDto> Segmentos { get; set; } = new();
}

public class SegmentoParaRealizarDto
{
    public int Ordem { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string? Conteudo { get; set; }
    public int? SegmentoId { get; set; }
}