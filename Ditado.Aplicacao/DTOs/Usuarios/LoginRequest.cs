using System.ComponentModel.DataAnnotations;

namespace Ditado.Aplicacao.DTOs.Usuarios;

public class LoginRequest
{
    [Required(ErrorMessage = "Login é obrigatório.")]
    [EmailAddress(ErrorMessage = "Login deve ser um email válido.")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Senha é obrigatória.")]
    public string Senha { get; set; } = string.Empty;
}