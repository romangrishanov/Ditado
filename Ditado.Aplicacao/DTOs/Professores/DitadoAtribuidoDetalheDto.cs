namespace Ditado.Aplicacao.DTOs.Professores;

public class DitadoAtribuidoDetalheDto
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
    
    // Dados da Atribuição
    public DateTime DataAtribuicao { get; set; }
    public DateTime DataLimite { get; set; }
    
    // Estatísticas Gerais
    public int TotalAlunos { get; set; }
    public int AlunosQueFizeram { get; set; }
    public decimal PercentualConclusao { get; set; }
    public decimal? NotaMedia { get; set; }
    
    // Lista de Alunos
    public List<AlunoResultadoDto> Alunos { get; set; } = new();
    
    // Gráfico de Erros
    public List<ErrosPorTipoDto> ErrosPorTipo { get; set; } = new();
}