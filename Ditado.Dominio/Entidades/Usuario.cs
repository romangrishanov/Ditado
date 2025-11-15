using Ditado.Dominio.Enums;

namespace Ditado.Dominio.Entidades;

public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public string? Matricula { get; set; }
    public TipoUsuario Tipo { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataUltimoAcesso { get; set; }
}