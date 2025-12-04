namespace Ditado.Dominio.Entidades;

public class Ditado
{
    public const string AudioMimeTypePadrao = "audio/mpeg";
    
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public byte[] AudioLeitura { get; set; } = Array.Empty<byte>();
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public bool Ativo { get; set; } = true;
    
    // Autor do ditado (Professor ou Admin que criou)
    public int? AutorId { get; set; }
    public Usuario? Autor { get; set; }
    
    public ICollection<DitadoSegmento> Segmentos { get; set; } = new List<DitadoSegmento>();
    public ICollection<RespostaDitado> Respostas { get; set; } = new List<RespostaDitado>();
    public ICollection<DitadoCategoria> DitadoCategorias { get; set; } = new List<DitadoCategoria>();
    public ICollection<TurmaDitado> TurmaDitados { get; set; } = new List<TurmaDitado>();
}