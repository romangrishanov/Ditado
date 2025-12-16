using Ditado.Aplicacao.DTOs.Professores;
using Ditado.Aplicacao.DTOs;
using Ditado.Dominio.Enums;
using Ditado.Dominio.Extensions;
using Ditado.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Ditado.Aplicacao.Services;

public class ProfessorService
{
    private readonly DitadoDbContext _context;

    public ProfessorService(DitadoDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lista todos os ditados atribuídos pelo professor logado
    /// </summary>
    public async Task<List<DitadoAtribuidoResumoDto>> ListarMeusDitadosAtribuidosAsync(int professorId)
    {
        var agora = DateTime.UtcNow;

        // Buscar turmas do professor
        var turmasIds = await _context.Turmas
            .Where(t => t.ProfessorResponsavelId == professorId)
            .Select(t => t.Id)
            .ToListAsync();

        if (!turmasIds.Any())
            return new List<DitadoAtribuidoResumoDto>();

        // Buscar atribuições de ditados
        var atribuicoes = await _context.TurmaDitados
            .Where(td => turmasIds.Contains(td.TurmaId))
            .Include(td => td.Turma)
            .Include(td => td.Ditado)
            .ThenInclude(d => d.DitadoCategorias)
            .ThenInclude(dc => dc.Categoria)
            .ToListAsync();

        var resultado = new List<DitadoAtribuidoResumoDto>();

        foreach (var atribuicao in atribuicoes)
        {
            // Total de alunos na turma
            var totalAlunos = await _context.Usuarios
                .Where(u => u.TurmasComoAluno.Any(t => t.Id == atribuicao.TurmaId))
                .CountAsync();

            // Buscar APENAS primeira tentativa de cada aluno
            var primeiraTentativaPorAluno = await _context.RespostaDitados
                .Where(r => r.DitadoId == atribuicao.DitadoId)
                .Where(r => r.Aluno.TurmasComoAluno.Any(t => t.Id == atribuicao.TurmaId))
                .GroupBy(r => r.AlunoId)
                .Select(g => g.OrderBy(r => r.DataRealizacao).First())
                .ToListAsync();

            var alunosQueFizeram = primeiraTentativaPorAluno.Count;
            var notaMedia = primeiraTentativaPorAluno.Any() 
                ? primeiraTentativaPorAluno.Average(r => r.Nota) 
                : (decimal?)null;

            resultado.Add(new DitadoAtribuidoResumoDto
            {
                TurmaId = atribuicao.TurmaId,
                TurmaNome = atribuicao.Turma.Nome,
                Serie = atribuicao.Turma.Serie,
                AnoLetivo = atribuicao.Turma.AnoLetivo,
                DitadoId = atribuicao.DitadoId,
                DitadoTitulo = atribuicao.Ditado.Titulo,
                DitadoDescricao = atribuicao.Ditado.Descricao,
                Categorias = atribuicao.Ditado.DitadoCategorias
                    .Select(dc => dc.Categoria.Nome)
                    .ToList(),
                DataAtribuicao = atribuicao.DataAtribuicao,
                DataLimite = atribuicao.DataLimite,
                Vencido = agora > atribuicao.DataLimite,
                TotalAlunos = totalAlunos,
                AlunosQueFizeram = alunosQueFizeram,
                PercentualConclusao = totalAlunos > 0 
                    ? Math.Round((decimal)alunosQueFizeram / totalAlunos * 100, 2) 
                    : 0,
                NotaMedia = notaMedia.HasValue 
                    ? Math.Round(notaMedia.Value, 2) 
                    : null
            });
        }

        // Ordenar por data limite ASC (vencidos primeiro, depois os próximos)
        return resultado.OrderBy(r => r.DataLimite).ToList();
    }

    /// <summary>
    /// Obtém detalhes de um ditado atribuído a uma turma
    /// </summary>
    public async Task<DitadoAtribuidoDetalheDto?> ObterDetalhesAtribuicaoAsync(int turmaId, int ditadoId, int professorId)
    {
        // Verificar se a turma pertence ao professor
        var turma = await _context.Turmas
            .Include(t => t.Alunos)
            .FirstOrDefaultAsync(t => t.Id == turmaId && t.ProfessorResponsavelId == professorId);

        if (turma == null)
            return null;

        // Buscar atribuição com segmentos do ditado
        var atribuicao = await _context.TurmaDitados
            .Include(td => td.Ditado)
            .ThenInclude(d => d.Segmentos)
            .FirstOrDefaultAsync(td => td.TurmaId == turmaId && td.DitadoId == ditadoId);

        if (atribuicao == null)
            return null;

        // Buscar todos os alunos da turma
        var alunosDaTurma = await _context.Usuarios
            .Where(u => u.TurmasComoAluno.Any(t => t.Id == turmaId))
            .ToListAsync();

        // Buscar APENAS primeira tentativa de cada aluno
        var primeiraTentativas = await _context.RespostaDitados
            .Where(r => r.DitadoId == ditadoId)
            .Where(r => alunosDaTurma.Select(a => a.Id).Contains(r.AlunoId))
            .Include(r => r.RespostasSegmentos)
            .ThenInclude(rs => rs.Segmento)
            .GroupBy(r => r.AlunoId)
            .Select(g => g.OrderBy(r => r.DataRealizacao).First())
            .ToListAsync();

        var primeiraTentativasPorAluno = primeiraTentativas.ToDictionary(r => r.AlunoId);

        // Montar lista de alunos
        var alunosResultado = new List<AlunoResultadoDto>();

        foreach (var aluno in alunosDaTurma)
        {
            var fez = primeiraTentativasPorAluno.ContainsKey(aluno.Id);
            var resposta = fez ? primeiraTentativasPorAluno[aluno.Id] : null;

            string? erroMaisComum = null;
            if (resposta != null)
            {
                var erros = resposta.RespostasSegmentos
                    .Where(rs => !rs.Correto && rs.TipoErro.HasValue)
                    .GroupBy(rs => rs.TipoErro!.Value)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();

                if (erros != null)
                {
                    erroMaisComum = erros.Key.ObterDescricao();
                }
            }

            alunosResultado.Add(new AlunoResultadoDto
            {
                AlunoId = aluno.Id,
                Nome = aluno.Nome,
                Matricula = aluno.Matricula,
                Fez = fez,
                DataEntrega = resposta?.DataRealizacao,
                Nota = resposta != null ? Math.Round(resposta.Nota, 2) : null,
                ErroMaisComum = erroMaisComum,
                Atrasado = resposta != null && resposta.DataRealizacao > atribuicao.DataLimite
            });
        }

        // Calcular erros por tipo (agregando toda a turma, só primeira tentativa)
        var todosErros = primeiraTentativas
            .SelectMany(r => r.RespostasSegmentos)
            .Where(rs => !rs.Correto && rs.TipoErro.HasValue)
            .GroupBy(rs => rs.TipoErro!.Value)
            .Select(g => new ErrosPorTipoDto
            {
                TipoErroId = (int)g.Key,
                Descricao = g.Key.ObterDescricao(),
                DescricaoCurta = g.Key.ObterDescricaoCurta(),
                Quantidade = g.Count()
            })
            .OrderByDescending(e => e.Quantidade)
            .ToList();

        // Calcular erros por palavra (apenas lacunas, 1ª tentativa)
        var lacunas = atribuicao.Ditado.Segmentos
            .Where(s => s.Tipo == TipoSegmento.Lacuna)
            .OrderBy(s => s.Ordem)
            .ToList();

        var errosPorPalavra = new List<ErrosPorPalavraDto>();
        var totalAlunosQueFizeram = primeiraTentativas.Count;

        foreach (var lacuna in lacunas)
        {
            // Contar quantos alunos erraram esta palavra (1ª tentativa)
            var quantidadeErros = primeiraTentativas
                .SelectMany(r => r.RespostasSegmentos)
                .Count(rs => rs.SegmentoId == lacuna.Id && !rs.Correto);

            errosPorPalavra.Add(new ErrosPorPalavraDto
            {
                Palavra = lacuna.Conteudo,
                QuantidadeErros = quantidadeErros,
                PercentualErro = totalAlunosQueFizeram > 0
                    ? Math.Round((decimal)quantidadeErros / totalAlunosQueFizeram * 100, 2)
                    : 0
            });
        }

        // Estatísticas gerais
        var totalAlunos = alunosDaTurma.Count;
        var alunosQueFizeram = primeiraTentativas.Count;
        var notaMedia = primeiraTentativas.Any()
            ? Math.Round(primeiraTentativas.Average(r => r.Nota), 2)
            : (decimal?)null;

        return new DitadoAtribuidoDetalheDto
        {
            TurmaId = turma.Id,
            TurmaNome = turma.Nome,
            Serie = turma.Serie,
            AnoLetivo = turma.AnoLetivo,
            DitadoId = atribuicao.DitadoId,
            DitadoTitulo = atribuicao.Ditado.Titulo,
            DitadoDescricao = atribuicao.Ditado.Descricao,
            DataAtribuicao = atribuicao.DataAtribuicao,
            DataLimite = atribuicao.DataLimite,
            TotalAlunos = totalAlunos,
            AlunosQueFizeram = alunosQueFizeram,
            PercentualConclusao = totalAlunos > 0
                ? Math.Round((decimal)alunosQueFizeram / totalAlunos * 100, 2)
                : 0,
            NotaMedia = notaMedia,
            Alunos = alunosResultado.OrderBy(a => a.Nome).ToList(),
            ErrosPorTipo = todosErros,
            ErrosPorPalavra = errosPorPalavra
        };
    }

    /// <summary>
    /// Obtém o resultado detalhado de um aluno específico em um ditado (1ª tentativa)
    /// </summary>
    public async Task<ResultadoAlunoDetalheDto?> ObterResultadoAlunoAsync(int turmaId, int alunoId, int ditadoId, int professorId)
    {
        // Verificar se a turma pertence ao professor
        var turma = await _context.Turmas
            .FirstOrDefaultAsync(t => t.Id == turmaId && t.ProfessorResponsavelId == professorId);

        if (turma == null)
            return null;

        // Verificar se o aluno está na turma
        var alunoNaTurma = await _context.Usuarios
            .AnyAsync(u => u.Id == alunoId && u.TurmasComoAluno.Any(t => t.Id == turmaId));

        if (!alunoNaTurma)
            return null;

        // Buscar a PRIMEIRA tentativa do aluno neste ditado
        var primeiraTentativa = await _context.RespostaDitados
            .Where(r => r.DitadoId == ditadoId && r.AlunoId == alunoId)
            .Include(r => r.RespostasSegmentos)
            .ThenInclude(rs => rs.Segmento)
            .Include(r => r.Aluno)
            .Include(r => r.Ditado)
            .OrderBy(r => r.DataRealizacao)
            .FirstOrDefaultAsync();

        if (primeiraTentativa == null)
            return null;

        // Montar lista de detalhes (igual ao que aluno viu)
        var detalhes = primeiraTentativa.RespostasSegmentos
            .OrderBy(rs => rs.Segmento.Ordem)
            .Select(rs => new DetalheRespostaDto
            {
                SegmentoId = rs.SegmentoId,
                RespostaFornecida = rs.RespostaFornecida,
                RespostaEsperada = rs.Segmento.Conteudo,
                Correto = rs.Correto,
                TipoErro = rs.TipoErro.HasValue ? rs.TipoErro.Value.ObterDescricao() : null
            })
            .ToList();

        var totalLacunas = primeiraTentativa.RespostasSegmentos.Count;
        var acertos = primeiraTentativa.RespostasSegmentos.Count(rs => rs.Correto);

        return new ResultadoAlunoDetalheDto
        {
            AlunoId = primeiraTentativa.AlunoId,
            NomeAluno = primeiraTentativa.Aluno.Nome,
            Matricula = primeiraTentativa.Aluno.Matricula,
            DataRealizacao = primeiraTentativa.DataRealizacao,
            DitadoId = primeiraTentativa.DitadoId,
            DitadoTitulo = primeiraTentativa.Ditado.Titulo,
            DitadoDescricao = primeiraTentativa.Ditado.Descricao,
            Nota = Math.Round(primeiraTentativa.Nota, 2),
            TotalLacunas = totalLacunas,
            Acertos = acertos,
            Erros = totalLacunas - acertos,
            Detalhes = detalhes
        };
    }
}