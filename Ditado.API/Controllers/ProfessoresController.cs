using Ditado.Aplicacao.DTOs.Professores;
using Ditado.Aplicacao.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ditado.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Professor")]
[Produces("application/json")]
public class ProfessoresController : ControllerBase
{
    private readonly ProfessorService _professorService;

    public ProfessoresController(ProfessorService professorService)
    {
        _professorService = professorService;
    }

    /// <summary>
    /// Lista todos os ditados atribuídos pelo professor logado
    /// </summary>
    /// <remarks>
    /// Retorna uma lista de todos os ditados que o professor logado atribuiu a suas turmas.
    /// 
    /// **Ordenação:** Data limite ASC (vencidos primeiro)
    /// 
    /// **Inclui:**
    /// - Dados da turma (nome, série, ano letivo)
    /// - Dados do ditado (título, descrição, categorias)
    /// - Data de atribuição e data limite
    /// - **% de conclusão** (quantos alunos já fizeram)
    /// - **Nota média** (baseada na 1ª tentativa de cada aluno)
    /// 
    /// **Critério:** Sempre considera apenas a **primeira tentativa** de cada aluno
    /// 
    /// **Exemplo de resposta:**
    /// 
    ///     [
    ///       {
    ///         "turmaId": 5,
    ///         "turmaNome": "5º Ano A",
    ///         "ditadoId": 10,
    ///         "ditadoTitulo": "Ortografia Básica",
    ///         "dataLimite": "2024-12-10T23:59:59Z",
    ///         "vencido": true,
    ///         "totalAlunos": 25,
    ///         "alunosQueFizeram": 18,
    ///         "percentualConclusao": 72.0,
    ///         "notaMedia": 78.5
    ///       }
    ///     ]
    /// </remarks>
    /// <returns>Lista de ditados atribuídos</returns>
    /// <response code="200">Lista retornada com sucesso</response>
    /// <response code="401">Não autenticado</response>
    /// <response code="403">Sem permissão (apenas Professor e Admin)</response>
    [HttpGet("meus-ditados-atribuidos")]
    [ProducesResponseType(typeof(List<DitadoAtribuidoResumoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<DitadoAtribuidoResumoDto>>> ListarMeusDitadosAtribuidos()
    {
        var professorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var resultado = await _professorService.ListarMeusDitadosAtribuidosAsync(professorId);
        return Ok(resultado);
    }

    /// <summary>
    /// Obtém detalhes de um ditado atribuído a uma turma (drill-down)
    /// </summary>
    /// <remarks>
    /// Retorna informações detalhadas sobre os resultados dos alunos em um ditado específico.
    /// 
    /// **Inclui:**
    /// 
    /// **1. Lista de Alunos:**
    /// - Nome, matrícula
    /// - Se fez o ditado
    /// - Data de entrega (1ª tentativa)
    /// - Nota (1ª tentativa)
    /// - Erro mais comum
    /// - Se entregou atrasado
    /// 
    /// **2. Gráfico de Erros por Tipo:**
    /// - Agregação de todos os erros da turma
    /// - Considera apenas 1ª tentativa de cada aluno
    /// - Formato: `[{ tipoErroId, descricao, descricaoCurta, quantidade }]`
    /// 
    /// **3. Gráfico de Erros por Palavra:**
    /// - Lista de palavras (lacunas) do ditado
    /// - Quantidade de alunos que erraram cada palavra
    /// - Percentual de erro em relação aos alunos que fizeram
    /// - Considera apenas 1ª tentativa de cada aluno
    /// - Ordenado pela ordem das palavras no ditado
    /// 
    /// **Critério:** Sempre considera apenas a **primeira tentativa** de cada aluno
    /// 
    /// **Exemplo de resposta:**
    /// 
    ///     {
    ///       "turmaId": 5,
    ///       "turmaNome": "5º Ano A",
    ///       "ditadoId": 10,
    ///       "ditadoTitulo": "Ortografia Básica",
    ///       "totalAlunos": 25,
    ///       "alunosQueFizeram": 18,
    ///       "percentualConclusao": 72.0,
    ///       "notaMedia": 78.5,
    ///       "alunos": [
    ///         {
    ///           "alunoId": 100,
    ///           "nome": "João Silva",
    ///           "matricula": "2024001",
    ///           "fez": true,
    ///           "dataEntrega": "2024-12-08T14:30:00Z",
    ///           "nota": 85.5,
    ///           "erroMaisComum": "Erro de acentuação",
    ///           "atrasado": false
    ///         }
    ///       ],
    ///       "errosPorTipo": [
    ///         {
    ///           "tipoErroId": 2,
    ///           "descricao": "Erro de acentuação",
    ///           "descricaoCurta": "Acentuação",
    ///           "quantidade": 12
    ///         },
    ///         {
    ///           "tipoErroId": 1,
    ///           "descricao": "Erro ortográfico",
    ///           "descricaoCurta": "Ortografia",
    ///           "quantidade": 8
    ///         }
    ///       ],
    ///       "errosPorPalavra": [
    ///         {
    ///           "palavra": "cachorro",
    ///           "quantidadeErros": 12,
    ///           "percentualErro": 66.67
    ///         },
    ///         {
    ///           "palavra": "árvore",
    ///           "quantidadeErros": 15,
    ///           "percentualErro": 83.33
    ///         },
    ///         {
    ///           "palavra": "três",
    ///           "quantidadeErros": 8,
    ///           "percentualErro": 44.44
    ///         }
    ///       ]
    ///     }
    /// </remarks>
    /// <param name="turmaId">ID da turma</param>
    /// <param name="ditadoId">ID do ditado</param>
    /// <returns>Detalhes da atribuição com resultados dos alunos</returns>
    /// <response code="200">Detalhes retornados com sucesso</response>
    /// <response code="401">Não autenticado</response>
    /// <response code="403">Sem permissão (turma não pertence ao professor)</response>
    /// <response code="404">Atribuição não encontrada</response>
    [HttpGet("turmas/{turmaId}/ditados/{ditadoId}/resultados")]
    [ProducesResponseType(typeof(DitadoAtribuidoDetalheDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DitadoAtribuidoDetalheDto>> ObterDetalhesAtribuicao(int turmaId, int ditadoId)
    {
        var professorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var resultado = await _professorService.ObterDetalhesAtribuicaoAsync(turmaId, ditadoId, professorId);

        if (resultado == null)
            return NotFound(new { mensagem = "Atribuição não encontrada ou você não tem permissão para visualizá-la." });

        return Ok(resultado);
    }

    /// <summary>
    /// Obtém o resultado detalhado de um aluno específico em um ditado
    /// </summary>
    /// <remarks>
    /// Retorna exatamente o que o aluno viu ao submeter o ditado (1ª tentativa):
    /// - Lista completa de respostas (palavra esperada vs palavra fornecida)
    /// - Tipo de erro em cada palavra
    /// - Estatísticas (nota, acertos, erros)
    /// 
    /// **Critério:** Sempre considera apenas a **primeira tentativa** do aluno
    /// 
    /// **Acesso:**
    /// - Apenas o professor responsável pela turma pode visualizar
    /// 
    /// **Exemplo de resposta:**
    /// 
    ///     {
    ///       "alunoId": 100,
    ///       "nomeAluno": "João Silva",
    ///       "matricula": "2024001",
    ///       "dataRealizacao": "2024-12-08T14:30:00Z",
    ///       "ditadoId": 10,
    ///       "ditadoTitulo": "Ortografia Básica",
    ///       "ditadoDescricao": "Ditado sobre uso de SS, S, Z",
    ///       "nota": 75.0,
    ///       "totalLacunas": 4,
    ///       "acertos": 3,
    ///       "erros": 1,
    ///       "detalhes": [
    ///         {
    ///           "segmentoId": 45,
    ///           "respostaFornecida": "cachorro",
    ///           "respostaEsperada": "cachorro",
    ///           "correto": true,
    ///           "tipoErro": null
    ///         },
    ///         {
    ///           "segmentoId": 46,
    ///           "respostaFornecida": "arvore",
    ///           "respostaEsperada": "árvore",
    ///           "correto": false,
    ///           "tipoErro": "Erro de acentuação"
    ///         },
    ///         {
    ///           "segmentoId": 47,
    ///           "respostaFornecida": "tres",
    ///           "respostaEsperada": "três",
    ///           "correto": false,
    ///           "tipoErro": "Erro de acentuação"
    ///         },
    ///         {
    ///           "segmentoId": 48,
    ///           "respostaFornecida": "gato",
    ///           "respostaEsperada": "gato",
    ///           "correto": true,
    ///           "tipoErro": null
    ///         }
    ///       ]
    ///     }
    /// </remarks>
    /// <param name="turmaId">ID da turma</param>
    /// <param name="alunoId">ID do aluno</param>
    /// <param name="ditadoId">ID do ditado</param>
    /// <returns>Resultado detalhado do aluno (1ª tentativa)</returns>
    /// <response code="200">Resultado retornado com sucesso</response>
    /// <response code="401">Não autenticado</response>
    /// <response code="403">Sem permissão (turma não pertence ao professor)</response>
    /// <response code="404">Aluno não encontrado na turma ou não fez o ditado</response>
    [HttpGet("turmas/{turmaId}/alunos/{alunoId}/ditados/{ditadoId}/resultado")]
    [ProducesResponseType(typeof(ResultadoAlunoDetalheDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultadoAlunoDetalheDto>> ObterResultadoAluno(int turmaId, int alunoId, int ditadoId)
    {
        var professorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var resultado = await _professorService.ObterResultadoAlunoAsync(turmaId, alunoId, ditadoId, professorId);

        if (resultado == null)
            return NotFound(new 
            { 
                mensagem = "Resultado não encontrado. Verifique se a turma pertence a você, se o aluno está na turma e se ele realizou o ditado." 
            });

        return Ok(resultado);
    }
}