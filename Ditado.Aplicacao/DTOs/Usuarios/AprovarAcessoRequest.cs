using System.ComponentModel.DataAnnotations;
using Ditado.Dominio.Enums;

namespace Ditado.Aplicacao.DTOs.Usuarios;

public class AprovarAcessoRequest
{
    [Required(ErrorMessage = "Tipo de usuário é obrigatório.")]
    public TipoUsuario NovoTipo { get; set; }
}