namespace Ditado.Aplicacao.DTOs;

/// <summary>
/// Representação completa de um ditado (com palavras das lacunas visíveis)
/// Usado para visualização por professores/administradores
/// </summary>
public class DitadoCompletoDto
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public DateTime DataCriacao { get; set; }
    
    /// <summary>
    /// ID do autor (professor/administrador)
    /// </summary>
    public int? AutorId { get; set; }
    
    /// <summary>
    /// Nome do autor
    /// </summary>
    public string? AutorNome { get; set; }
    
    /// <summary>
    /// Categorias associadas ao ditado
    /// </summary>
    public List<CategoriaSimplificadaDto> Categorias { get; set; } = new();
    
    /// <summary>
    /// Áudio do ditado em base64 (data:audio/mpeg;base64,...)
    /// </summary>
    public string AudioBase64 { get; set; } = string.Empty;
    
    /// <summary>
    /// Segmentos do ditado (texto e lacunas COM palavras visíveis)
    /// </summary>
    public List<SegmentoCompletoDto> Segmentos { get; set; } = new();
}

/// <summary>
/// Segmento do ditado com todas as informações visíveis
/// </summary>
public class SegmentoCompletoDto
{
    /// <summary>
    /// ID do segmento
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Ordem do segmento no ditado
    /// </summary>
    public int Ordem { get; set; }
    
    /// <summary>
    /// Tipo: "Texto" ou "Lacuna"
    /// </summary>
    public string Tipo { get; set; } = string.Empty;
    
    /// <summary>
    /// Conteúdo do segmento (sempre visível, inclusive para lacunas)
    /// </summary>
    public string Conteudo { get; set; } = string.Empty;
}