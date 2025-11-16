using System.ComponentModel.DataAnnotations;
using Ditado.Dominio.Enums;

namespace Ditado.Aplicacao.DTOs.Usuarios;

public class AtualizarUsuarioRequest
{
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Nome deve ter entre 3 e 200 caracteres.")]
    public string? Nome { get; set; }
    
    [StringLength(20, ErrorMessage = "Matrícula não pode exceder 20 caracteres.")]
    public string? Matricula { get; set; }
    
    public string? SenhaAtual { get; set; }
    
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Senha nova deve ter no mínimo 6 caracteres.")]
    public string? SenhaNova { get; set; }
    
    public TipoUsuario? Tipo { get; set; }

    public bool? Ativo { get; set; }
}