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
    
    public ICollection<DitadoSegmento> Segmentos { get; set; } = new List<DitadoSegmento>();
    public ICollection<RespostaDitado> Respostas { get; set; } = new List<RespostaDitado>();
}