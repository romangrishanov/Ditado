namespace Ditado.Aplicacao.DTOs.Professores;

/// <summary>
/// Detalhes do resultado de um aluno específico em um ditado
/// Usado pelo professor para visualizar como o aluno respondeu
/// </summary>
public class ResultadoAlunoDetalheDto
{
    // Dados do Aluno
    public int AlunoId { get; set; }
    public string NomeAluno { get; set; } = string.Empty;
    public string? Matricula { get; set; }
    public DateTime DataRealizacao { get; set; }
    
    // Dados do Ditado
    public int DitadoId { get; set; }
    public string DitadoTitulo { get; set; } = string.Empty;
    public string? DitadoDescricao { get; set; }
    
    // Estatísticas
    public decimal Nota { get; set; }
    public int TotalLacunas { get; set; }
    public int Acertos { get; set; }
    public int Erros { get; set; }
    
    // Detalhes (igual ao que aluno viu ao submeter)
    public List<DetalheRespostaDto> Detalhes { get; set; } = new();
}