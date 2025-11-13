namespace Ditado.Aplicacao.DTOs;

public class CriarDitadoRequest
{
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string TextoComMarcacoes { get; set; } = string.Empty;
    public string AudioBase64 { get; set; } = string.Empty;
}