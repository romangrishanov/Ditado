using System.ComponentModel.DataAnnotations;
using Ditado.Dominio.Enums;

namespace Ditado.Aplicacao.DTOs.Usuarios;

public class CriarUsuarioRequest
{
    [Required(ErrorMessage = "Nome é obrigatório.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Nome deve ter entre 3 e 200 caracteres.")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Login (email) é obrigatório.")]
    [EmailAddress(ErrorMessage = "Login deve ser um email válido.")]
    [StringLength(100, ErrorMessage = "Login não pode exceder 100 caracteres.")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Senha é obrigatória.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Senha deve ter no mínimo 6 caracteres.")]
    public string Senha { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Matrícula não pode exceder 20 caracteres.")]
    public string? Matricula { get; set; }

    // usado apenas quando Admin cria usuário diretamente
    public TipoUsuario? Tipo { get; set; }
}