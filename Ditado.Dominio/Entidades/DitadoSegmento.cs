using Ditado.Dominio.Enums;

namespace Ditado.Dominio.Entidades;

public class DitadoSegmento
{
    public int Id { get; set; }
    public int DitadoId { get; set; }
    public int Ordem { get; set; }
    public TipoSegmento Tipo { get; set; }
    public string Conteudo { get; set; } = string.Empty;
    
    public Ditado Ditado { get; set; } = null!;
    public ICollection<RespostaSegmento> Respostas { get; set; } = new List<RespostaSegmento>();
}