namespace Ditado.Aplicacao.DTOs.Professores;

/// <summary>
/// Estatísticas de erros por palavra (lacuna) do ditado
/// </summary>
public class ErrosPorPalavraDto
{
    /// <summary>
    /// Palavra do ditado (conteúdo da lacuna)
    /// </summary>
    public string Palavra { get; set; } = string.Empty;
    
    /// <summary>
    /// Quantidade de alunos que erraram esta palavra (1ª tentativa)
    /// </summary>
    public int QuantidadeErros { get; set; }
    
    /// <summary>
    /// Percentual de alunos que erraram esta palavra
    /// </summary>
    /// <example>68.50</example>
    public decimal PercentualErro { get; set; }
}