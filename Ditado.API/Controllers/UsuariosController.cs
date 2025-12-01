using System.Security.Claims;
using Ditado.Aplicacao.DTOs.Usuarios;
using Ditado.Aplicacao.Services;
using Ditado.Dominio.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ditado.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsuariosController : ControllerBase
{
	private readonly UsuarioService _usuarioService;

	public UsuariosController(UsuarioService usuarioService)
	{
		_usuarioService = usuarioService;
	}

	/// <summary>
	/// Cria um novo usuário (Admin apenas)
	/// </summary>
	/// <remarks>
	/// Apenas Administradores podem criar usuários diretamente no sistema.
	/// O tipo de usuário (Aluno, Professor, Administrador) é definido no momento da criação.
	/// 
	/// Para auto-cadastro de alunos, use o endpoint `/solicitar-acesso`.
	/// 
	/// Exemplo de requisição:
	/// 
	///     POST /api/usuarios
	///     {
	///        "nome": "João Silva",
	///        "login": "joao.silva@escola.com",
	///        "senha": "senha123",
	///        "matricula": "2024001",
	///        "tipo": "Aluno"
	///     }
	/// </remarks>
	/// <param name="request">Dados do usuário a ser criado</param>
	/// <returns>Usuário criado</returns>
	/// <response code="201">Usuário criado com sucesso</response>
	/// <response code="400">Dados inválidos (email duplicado, tipo inválido, etc.)</response>
	/// <response code="401">Não autenticado</response>
	/// <response code="403">Sem permissão (apenas Administrador)</response>
	[HttpPost]
	[Authorize(Roles = "Administrador")]
	[ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<UsuarioResponse>> CriarUsuario([FromBody] CriarUsuarioRequest request)
	{
		try
		{
			var usuario = await _usuarioService.CriarUsuarioAsync(request);
			return CreatedAtAction(nameof(ObterUsuario), new { id = usuario.Id }, usuario);
		}
		catch (InvalidOperationException ex)
		{
			return BadRequest(new { mensagem = ex.Message });
		}
	}

	/// <summary>
	/// Solicita acesso ao sistema (auto-cadastro)
	/// </summary>
	/// <remarks>
	/// Endpoint público para que novos usuários solicitem acesso.
	/// O usuário é criado com tipo "AcessoSolicitado" e status inativo.
	/// 
	/// Após a solicitação:
	/// 1. Aguardar aprovação de um Administrador ou Professor
	/// 2. Após aprovação, o login será liberado com o tipo apropriado (Aluno, Professor)
	/// 
	/// Não é possível fazer login enquanto o acesso não for aprovado.
	/// 
	/// Exemplo de requisição:
	/// 
	///     POST /api/usuarios/solicitar-acesso
	///     {
	///        "nome": "Maria Santos",
	///        "login": "maria.santos@escola.com",
	///        "senha": "senhaSegura123",
	///        "matricula": "2024002"
	///     }
	/// </remarks>
	/// <param name="request">Dados do usuário solicitante</param>
	/// <returns>Confirmação de solicitação enviada</returns>
	/// <response code="200">Solicitação enviada com sucesso</response>
	/// <response code="400">Dados inválidos (email já cadastrado, etc.)</response>
	[HttpPost("solicitar-acesso")]
	[AllowAnonymous]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<ActionResult<UsuarioResponse>> SolicitarAcesso([FromBody] CriarUsuarioRequest request)
	{
		try
		{
			var usuario = await _usuarioService.SolicitarAcessoAsync(request);
			return Ok(new
			{
				mensagem = "Solicitação de acesso enviada com sucesso! Aguarde a aprovação de um administrador ou professor.",
				usuario
			});
		}
		catch (InvalidOperationException ex)
		{
			return BadRequest(new { mensagem = ex.Message });
		}
	}

	/// <summary>
	/// Lista solicitações de acesso pendentes
	/// </summary>
	/// <remarks>
	/// Retorna todos os usuários com tipo "AcessoSolicitado" e status inativo,
	/// aguardando aprovação de um Administrador ou Professor.
	/// 
	/// Usado para:
	/// - Administradores e Professores revisarem novos cadastros
	/// - Aprovar ou rejeitar solicitações de acesso
	/// </remarks>
	/// <returns>Lista de solicitações pendentes</returns>
	/// <response code="200">Lista retornada com sucesso</response>
	/// <response code="401">Não autenticado</response>
	/// <response code="403">Sem permissão (apenas Admin e Professor)</response>
	[HttpGet("solicitacoes-pendentes")]
	[Authorize(Roles = "Administrador,Professor")]
	[ProducesResponseType(typeof(List<UsuarioResponse>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<List<UsuarioResponse>>> ListarSolicitacoesPendentes()
	{
		var solicitacoes = await _usuarioService.ListarSolicitacoesPendentesAsync();
		return Ok(solicitacoes);
	}

	/// <summary>
	/// Aprova uma solicitação de acesso
	/// </summary>
	/// <remarks>
	/// Permite que Administradores e Professores aprovem solicitações de acesso pendentes.
	/// 
	/// **Regras de aprovação:**
	/// - **Administrador**: Pode aprovar para qualquer tipo (Aluno, Professor, Administrador)
	/// - **Professor**: Pode aprovar apenas como Aluno ou Professor (não pode criar Administradores)
	/// 
	/// Após aprovação:
	/// - Usuário fica ativo
	/// - Tipo é alterado conforme especificado
	/// - Usuário pode fazer login no sistema
	/// 
	/// Exemplo de requisição:
	/// 
	///     POST /api/usuarios/5/aprovar-acesso
	///     {
	///        "novoTipo": "Aluno"
	///     }
	/// </remarks>
	/// <param name="id">ID do usuário a ser aprovado</param>
	/// <param name="request">Novo tipo de usuário após aprovação</param>
	/// <returns>Usuário aprovado</returns>
	/// <response code="200">Acesso aprovado com sucesso</response>
	/// <response code="400">Dados inválidos (tipo não permitido, usuário já aprovado, etc.)</response>
	/// <response code="401">Não autenticado</response>
	/// <response code="403">Sem permissão (apenas Admin e Professor)</response>
	/// <response code="404">Usuário não encontrado</response>
	[HttpPost("{id}/aprovar-acesso")]
	[Authorize(Roles = "Administrador,Professor")]
	[ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult<UsuarioResponse>> AprovarAcesso(int id, [FromBody] AprovarAcessoRequest request)
	{
		try
		{
			var tipoUsuarioLogado = User.FindFirst(ClaimTypes.Role)?.Value;
			var tipoEnum = Enum.Parse<TipoUsuario>(tipoUsuarioLogado ?? "Aluno");

			var usuario = await _usuarioService.AprovarAcessoAsync(id, request, tipoEnum);

			if (usuario == null)
				return NotFound(new { mensagem = "Usuário não encontrado." });

			return Ok(new
			{
				mensagem = "Acesso aprovado com sucesso!",
				usuario
			});
		}
		catch (InvalidOperationException ex)
		{
			return BadRequest(new { mensagem = ex.Message });
		}
	}

	/// <summary>
	/// Faz login no sistema
	/// </summary>
	/// <remarks>
	/// Autentica um usuário e retorna um token JWT para acesso aos endpoints protegidos.
	/// 
	/// **Requisitos para login:**
	/// - Email e senha válidos
	/// - Usuário com status ativo (não pode estar bloqueado)
	/// - Acesso já aprovado (não pode ter tipo "AcessoSolicitado")
	/// 
	/// O token JWT deve ser enviado no header `Authorization: Bearer {token}` nas requisições subsequentes.
	/// 
	/// Exemplo de requisição:
	/// 
	///     POST /api/usuarios/login
	///     {
	///        "login": "usuario@escola.com",
	///        "senha": "senha123"
	///     }
	///     
	/// Exemplo de resposta:
	/// 
	///     {
	///        "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
	///        "usuario": {
	///           "id": 1,
	///           "nome": "João Silva",
	///           "login": "usuario@escola.com",
	///           "tipo": "Aluno"
	///        }
	///     }
	/// </remarks>
	/// <param name="request">Credenciais de login</param>
	/// <returns>Token JWT e dados do usuário</returns>
	/// <response code="200">Login realizado com sucesso</response>
	/// <response code="401">Credenciais inválidas, acesso não aprovado ou conta inativa</response>
	[HttpPost("login")]
	[AllowAnonymous]
	[ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
	{
		try
		{
			var resultado = await _usuarioService.LoginAsync(request);

			if (resultado == null)
				return Unauthorized(new { mensagem = "Login ou senha inválidos." });

			return Ok(resultado);
		}
		catch (InvalidOperationException ex)
		{
			return Unauthorized(new { mensagem = ex.Message });
		}
	}

	/// <summary>
	/// Obtém um usuário por ID
	/// </summary>
	/// <remarks>
	/// Retorna os dados completos de um usuário específico.
	/// Qualquer usuário autenticado pode acessar este endpoint.
	/// </remarks>
	/// <param name="id">ID do usuário</param>
	/// <returns>Dados do usuário</returns>
	/// <response code="200">Usuário encontrado</response>
	/// <response code="401">Não autenticado</response>
	/// <response code="404">Usuário não encontrado</response>
	[HttpGet("{id}")]
	[Authorize]
	[ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult<UsuarioResponse>> ObterUsuario(int id)
	{
		var usuario = await _usuarioService.ObterPorIdAsync(id);

		if (usuario == null)
			return NotFound(new { mensagem = "Usuário não encontrado." });

		return Ok(usuario);
	}

	/// <summary>
	/// Lista todos os usuários
	/// </summary>
	/// <remarks>
	/// Retorna uma lista completa de todos os usuários cadastrados no sistema.
	/// Administradores e Professores podem acessar este endpoint.
	/// </remarks>
	/// <returns>Lista de usuários</returns>
	/// <response code="200">Lista retornada com sucesso</response>
	/// <response code="401">Não autenticado</response>
	/// <response code="403">Sem permissão (apenas Administrador e Professor)</response>
	[HttpGet]
	[Authorize(Roles = "Administrador,Professor")]
	[ProducesResponseType(typeof(List<UsuarioResponse>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<List<UsuarioResponse>>> ListarUsuarios()
	{
		var usuarios = await _usuarioService.ListarUsuariosAsync();
		return Ok(usuarios);
	}

	/// <summary>
	/// Atualiza um usuário
	/// </summary>
	/// <remarks>
	/// Permite atualizar dados de um usuário com diferentes níveis de permissão:
	/// 
	/// **Edição própria (qualquer usuário autenticado):**
	/// - Nome, Matrícula, Senha (com senha atual)
	/// - NÃO pode alterar próprio Tipo ou Status Ativo
	/// 
	/// **Edição de outros usuários:**
	/// 
	/// **Alterar Status Ativo:**
	/// - Aluno: Não pode alterar ninguém
	/// - Professor: Pode ativar/desativar apenas Alunos
	/// - Administrador: Pode ativar/desativar qualquer um
	/// 
	/// **Alterar Tipo:**
	/// - Aluno: Não pode alterar ninguém
	/// - Professor: Pode promover Aluno > Professor (não pode criar Administrador nem reduzir tipos)
	/// - Administrador: Pode promover ou reduzir qualquer tipo (exceto para "AcessoSolicitado")
	/// 
	/// Exemplo de requisição (edição própria):
	/// 
	///     PUT /api/usuarios/5
	///     {
	///        "nome": "João Silva Santos",
	///        "senhaAtual": "senha123",
	///        "senhaNova": "novaSenha456"
	///     }
	///     
	/// Exemplo de requisição (Admin alterando tipo):
	/// 
	///     PUT /api/usuarios/10
	///     {
	///        "tipo": "Professor",
	///        "ativo": true
	///     }
	/// </remarks>
	/// <param name="id">ID do usuário</param>
	/// <param name="request">Dados a serem atualizados</param>
	/// <returns>Usuário atualizado</returns>
	/// <response code="200">Usuário atualizado com sucesso</response>
	/// <response code="400">Dados inválidos (senha incorreta, permissão negada, etc.)</response>
	/// <response code="401">Não autenticado</response>
	/// <response code="404">Usuário não encontrado</response>
	[HttpPut("{id}")]
	[Authorize]
	[ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult<UsuarioResponse>> AtualizarUsuario(int id, [FromBody] AtualizarUsuarioRequest request)
	{
		try
		{
			// Obter ID e Tipo do usuário logado
			var usuarioLogadoId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
			var tipoUsuarioLogado = Enum.Parse<TipoUsuario>(User.FindFirst(ClaimTypes.Role)?.Value ?? "Aluno");

			var usuario = await _usuarioService.AtualizarUsuarioAsync(id, request, usuarioLogadoId, tipoUsuarioLogado);

			if (usuario == null)
				return NotFound(new { mensagem = "Usuário não encontrado." });

			return Ok(usuario);
		}
		catch (InvalidOperationException ex)
		{
			return BadRequest(new { mensagem = ex.Message });
		}
	}

	/// <summary>
	/// Bloqueia um usuário
	/// </summary>
	/// <remarks>
	/// Define o status do usuário como inativo, impedindo login no sistema.
	/// Apenas Administradores podem bloquear usuários.
	/// 
	/// O usuário bloqueado não será excluído, apenas ficará inativo.
	/// Use `/desbloquear` para reativar o acesso.
	/// </remarks>
	/// <param name="id">ID do usuário</param>
	/// <response code="204">Usuário bloqueado com sucesso</response>
	/// <response code="401">Não autenticado</response>
	/// <response code="403">Sem permissão (apenas Administrador)</response>
	/// <response code="404">Usuário não encontrado</response>
	[HttpPatch("{id}/bloquear")]
	[Authorize(Roles = "Administrador")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult> BloquearUsuario(int id)
	{
		var sucesso = await _usuarioService.BloquearUsuarioAsync(id);

		if (!sucesso)
			return NotFound(new { mensagem = "Usuário não encontrado." });

		return NoContent();
	}

	/// <summary>
	/// Desbloqueia um usuário
	/// </summary>
	/// <remarks>
	/// Reativa um usuário bloqueado, permitindo login novamente.
	/// Apenas Administradores podem desbloquear usuários.
	/// </remarks>
	/// <param name="id">ID do usuário</param>
	/// <response code="204">Usuário desbloqueado com sucesso</response>
	/// <response code="401">Não autenticado</response>
	/// <response code="403">Sem permissão (apenas Administrador)</response>
	/// <response code="404">Usuário não encontrado</response>
	[HttpPatch("{id}/desbloquear")]
	[Authorize(Roles = "Administrador")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult> DesbloquearUsuario(int id)
	{
		var sucesso = await _usuarioService.DesbloquearUsuarioAsync(id);

		if (!sucesso)
			return NotFound(new { mensagem = "Usuário não encontrado." });

		return NoContent();
	}

	/// <summary>
	/// Exclui um usuário permanentemente
	/// </summary>
	/// <remarks>
	/// Remove um usuário do sistema de forma irreversível.
	/// Apenas Administradores podem excluir usuários.
	/// 
	/// **ATENÇÃO**: Esta operação é permanente e não pode ser desfeita!
	/// Considere usar `/bloquear` ao invés de excluir para manter o histórico.
	/// </remarks>
	/// <param name="id">ID do usuário</param>
	/// <response code="204">Usuário excluído com sucesso</response>
	/// <response code="401">Não autenticado</response>
	/// <response code="403">Sem permissão (apenas Administrador)</response>
	/// <response code="404">Usuário não encontrado</response>
	[HttpDelete("{id}")]
	[Authorize(Roles = "Administrador")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult> DeletarUsuario(int id)
	{
		var sucesso = await _usuarioService.DeletarUsuarioAsync(id);

		if (!sucesso)
			return NotFound(new { mensagem = "Usuário não encontrado." });

		return NoContent();
	}
}