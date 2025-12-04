namespace Ditado.Aplicacao.DTOs.Categorias;

public class CategoriaResponse
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public int TotalDitados { get; set; }
}