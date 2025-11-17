using Ditado.Aplicacao.DTOs.Turmas;
using Ditado.Aplicacao.DTOs.Usuarios;
using Ditado.Dominio.Entidades;
using Ditado.Dominio.Enums;
using Ditado.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Ditado.Aplicacao.Services;

public class TurmaService
{
	private readonly DitadoDbContext _context;

	public TurmaService(DitadoDbContext context)
	{
		_context = context;
	}

	public async Task<TurmaResponse> CriarTurmaAsync(CriarTurmaRequest request)
	{
		// Validar professor
		var professor = await _context.Usuarios.FindAsync(request.ProfessorResponsavelId);
		if (professor == null || (professor.Tipo != TipoUsuario.Professor && professor.Tipo != TipoUsuario.Administrador))
			throw new InvalidOperationException("Professor responsável inválido. Deve ser um professor ou administrador.");

		// Validar alunos se fornecidos
		List<Usuario> alunos = new List<Usuario>();
		if (request.AlunosIds.Any())
		{
			alunos = await _context.Usuarios
				.Where(u => request.AlunosIds.Contains(u.Id) && u.Tipo == TipoUsuario.Aluno)
				.ToListAsync();

			if (alunos.Count != request.AlunosIds.Count)
				throw new InvalidOperationException("Um ou mais alunos são inválidos.");
		}

		var turma = new Turma
		{
			Nome = request.Nome,
			Serie = request.Serie,
			AnoLetivo = request.AnoLetivo,
			Semestre = request.Semestre,
			Descricao = request.Descricao,
			ProfessorResponsavelId = request.ProfessorResponsavelId,
			Alunos = alunos,
			Ativo = true,
			DataCriacao = DateTime.UtcNow
		};

		_context.Turmas.Add(turma);
		await _context.SaveChangesAsync();

		return await MapearParaResponseAsync(turma);
	}

	public async Task<TurmaResponse?> ObterPorIdAsync(int id)
	{
		var turma = await _context.Turmas
			.Include(t => t.ProfessorResponsavel)
			.Include(t => t.Alunos)
			.FirstOrDefaultAsync(t => t.Id == id);

		return turma == null ? null : await MapearParaResponseAsync(turma);
	}

	public async Task<List<TurmaResponse>> ListarTurmasAsync(bool? apenasAtivas = true)
	{
		var query = _context.Turmas
			.Include(t => t.ProfessorResponsavel)
			.Include(t => t.Alunos)
			.AsQueryable();

		if (apenasAtivas == true)
			query = query.Where(t => t.Ativo);

		var turmas = await query.OrderBy(t => t.Serie).ThenBy(t => t.Nome).ToListAsync();

		var responses = new List<TurmaResponse>();
		foreach (var turma in turmas)
		{
			responses.Add(await MapearParaResponseAsync(turma));
		}

		return responses;
	}

	public async Task<List<TurmaResponse>> ListarTurmasPorProfessorAsync(int professorId)
	{
		var turmas = await _context.Turmas
			.Include(t => t.ProfessorResponsavel)
			.Include(t => t.Alunos)
			.Where(t => t.ProfessorResponsavelId == professorId && t.Ativo)
			.OrderBy(t => t.Serie)
			.ThenBy(t => t.Nome)
			.ToListAsync();

		var responses = new List<TurmaResponse>();
		foreach (var turma in turmas)
		{
			responses.Add(await MapearParaResponseAsync(turma));
		}

		return responses;
	}

	public async Task<TurmaResponse?> AtualizarTurmaAsync(int id, AtualizarTurmaRequest request)
	{
		var turma = await _context.Turmas
			.Include(t => t.Alunos)
			.FirstOrDefaultAsync(t => t.Id == id);

		if (turma == null)
			return null;

		if (!string.IsNullOrWhiteSpace(request.Nome))
			turma.Nome = request.Nome;

		if (request.Serie.HasValue)
			turma.Serie = request.Serie.Value;

		if (request.AnoLetivo.HasValue)
			turma.AnoLetivo = request.AnoLetivo.Value;

		if (request.Semestre != null)
			turma.Semestre = string.IsNullOrWhiteSpace(request.Semestre) ? null : request.Semestre;

		if (request.Descricao != null)
			turma.Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao;

		if (request.ProfessorResponsavelId.HasValue)
		{
			var professor = await _context.Usuarios.FindAsync(request.ProfessorResponsavelId.Value);
			if (professor == null || (professor.Tipo != TipoUsuario.Professor && professor.Tipo != TipoUsuario.Administrador))
				throw new InvalidOperationException("Professor responsável inválido.");

			turma.ProfessorResponsavelId = request.ProfessorResponsavelId.Value;
		}

		if (request.AlunosIds != null)
		{
			var novosAlunos = await _context.Usuarios
				.Where(u => request.AlunosIds.Contains(u.Id) && u.Tipo == TipoUsuario.Aluno)
				.ToListAsync();

			if (novosAlunos.Count != request.AlunosIds.Count)
				throw new InvalidOperationException("Um ou mais alunos são inválidos.");

			turma.Alunos.Clear();
			foreach (var aluno in novosAlunos)
			{
				turma.Alunos.Add(aluno);
			}
		}

		if (request.Ativo.HasValue)
			turma.Ativo = request.Ativo.Value;

		await _context.SaveChangesAsync();

		return await MapearParaResponseAsync(turma);
	}

	public async Task<bool> ExcluirTurmaAsync(int id, int usuarioLogadoId, TipoUsuario tipoUsuarioLogado)
	{
		var turma = await _context.Turmas.FindAsync(id);
		if (turma == null)
			return false;

		// Professor só pode excluir suas próprias turmas
		if (tipoUsuarioLogado == TipoUsuario.Professor)
		{
			if (turma.ProfessorResponsavelId != usuarioLogadoId)
				throw new InvalidOperationException("Você só pode excluir turmas onde é responsável.");
		}

		// Admin pode excluir qualquer turma (sem validação adicional)

		_context.Turmas.Remove(turma);
		await _context.SaveChangesAsync();

		return true;
	}

	/// <summary>
	/// Adiciona um aluno a uma turma
	/// </summary>
	public async Task<TurmaResponse?> AdicionarAlunoAsync(int turmaId, int alunoId)
	{
		var turma = await _context.Turmas
			.Include(t => t.Alunos)
			.FirstOrDefaultAsync(t => t.Id == turmaId);

		if (turma == null)
			return null;

		// Verificar se aluno existe e é realmente um aluno
		var aluno = await _context.Usuarios.FindAsync(alunoId);
		if (aluno == null || aluno.Tipo != TipoUsuario.Aluno)
			throw new InvalidOperationException("Aluno não encontrado ou ID não corresponde a um aluno.");

		// Verificar se já está na turma
		if (turma.Alunos.Any(a => a.Id == alunoId))
			throw new InvalidOperationException("Aluno já está matriculado nesta turma.");

		turma.Alunos.Add(aluno);
		await _context.SaveChangesAsync();

		return await MapearParaResponseAsync(turma);
	}

	/// <summary>
	/// Remove um aluno de uma turma
	/// </summary>
	public async Task<TurmaResponse?> RemoverAlunoAsync(int turmaId, int alunoId)
	{
		var turma = await _context.Turmas
			.Include(t => t.Alunos)
			.FirstOrDefaultAsync(t => t.Id == turmaId);

		if (turma == null)
			return null;

		// Verificar se aluno está na turma
		var aluno = turma.Alunos.FirstOrDefault(a => a.Id == alunoId);
		if (aluno == null)
			throw new InvalidOperationException("Aluno não está matriculado nesta turma.");

		turma.Alunos.Remove(aluno);
		await _context.SaveChangesAsync();

		return await MapearParaResponseAsync(turma);
	}

	private async Task<TurmaResponse> MapearParaResponseAsync(Turma turma)
	{
		// Garantir que relacionamentos estão carregados
		if (turma.ProfessorResponsavel == null)
		{
			await _context.Entry(turma).Reference(t => t.ProfessorResponsavel).LoadAsync();
		}
		if (!turma.Alunos.Any())
		{
			await _context.Entry(turma).Collection(t => t.Alunos).LoadAsync();
		}

		return new TurmaResponse
		{
			Id = turma.Id,
			Nome = turma.Nome,
			Serie = turma.Serie,
			AnoLetivo = turma.AnoLetivo,
			Semestre = turma.Semestre,
			Descricao = turma.Descricao,
			Ativo = turma.Ativo,
			DataCriacao = turma.DataCriacao,
			ProfessorResponsavelId = turma.ProfessorResponsavelId,
			ProfessorResponsavelNome = turma.ProfessorResponsavel?.Nome ?? "N/A",
			TotalAlunos = turma.Alunos.Count,
			Alunos = turma.Alunos.Select(a => new UsuarioResponse
			{
				Id = a.Id,
				Nome = a.Nome,
				Login = a.Login,
				Matricula = a.Matricula,
				Tipo = a.Tipo.ToString(),
				Ativo = a.Ativo,
				DataCriacao = a.DataCriacao,
				DataUltimoAcesso = a.DataUltimoAcesso
			}).ToList()
		};
	}
}