namespace Ditado.Aplicacao.DTOs.Professores;

public class DitadoAtribuidoResumoDto
{
    // Dados da Turma
    public int TurmaId { get; set; }
    public string TurmaNome { get; set; } = string.Empty;
    public int Serie { get; set; }
    public int AnoLetivo { get; set; }
    
    // Dados do Ditado
    public int DitadoId { get; set; }
    public string DitadoTitulo { get; set; } = string.Empty;
    public string? DitadoDescricao { get; set; }
    public List<string> Categorias { get; set; } = new();
    
    // Dados da Atribuição
    public DateTime DataAtribuicao { get; set; }
    public DateTime DataLimite { get; set; }
    public bool Vencido { get; set; }
    
    // Estatísticas
    public int TotalAlunos { get; set; }
    public int AlunosQueFizeram { get; set; }
    public decimal PercentualConclusao { get; set; }
    public decimal? NotaMedia { get; set; } // Null se ninguém fez
}