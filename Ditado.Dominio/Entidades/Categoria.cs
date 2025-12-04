namespace Ditado.Dominio.Entidades;

public class Categoria
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    
    public ICollection<DitadoCategoria> DitadoCategorias { get; set; } = new List<DitadoCategoria>();
}