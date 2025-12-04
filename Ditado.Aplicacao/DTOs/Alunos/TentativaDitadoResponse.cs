namespace Ditado.Aplicacao.DTOs.Alunos;

public class TentativaDitadoResponse
{
    public int TentativaId { get; set; }
    public DateTime DataRealizacao { get; set; }
    public decimal Nota { get; set; }
    public int TotalLacunas { get; set; }
    public int Acertos { get; set; }
    public int Erros { get; set; }
    public bool Atrasado { get; set; }
}