using Ditado.Aplicacao.DTOs;
using Ditado.Dominio.Entidades;
using Ditado.Dominio.Enums;
using Ditado.Dominio.Extensions;
using Ditado.Infra.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Ditado.Aplicacao.Services;

public class DitadoService
{
	private readonly DitadoDbContext _context;
	private readonly CategoriaService _categoriaService; // Adicionado para gerenciar categorias

	public DitadoService(DitadoDbContext context, CategoriaService categoriaService)
	{
		_context = context;
		_categoriaService = categoriaService;
	}

	public async Task<DitadoResponse> CriarDitadoAsync(CriarDitadoRequest request, int autorId)
	{
		var segmentos = ParsearTextoComLacunas(request.TextoComMarcacoes);
		var audioDados = ExtrairAudioDeBase64(request.AudioBase64);

		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = request.Titulo,
			Descricao = request.Descricao,
			AudioLeitura = audioDados,
			DataCriacao = DateTime.UtcNow,
			Ativo = true,
			AutorId = autorId,
			Segmentos = segmentos
		};

		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();

		// ADICIONAR: Associar categorias ao ditado
		if (request.CategoriaIds != null && request.CategoriaIds.Any())
		{
			await _categoriaService.ValidarCategoriasAsync(request.CategoriaIds);

			foreach (var categoriaId in request.CategoriaIds.Distinct())
			{
				_context.DitadoCategorias.Add(new DitadoCategoria
				{
					DitadoId = ditado.Id,
					CategoriaId = categoriaId,
					DataAssociacao = DateTime.UtcNow
				});
			}

			await _context.SaveChangesAsync();
		}

		return await ObterDitadoComCategoriasAsync(ditado.Id);
	}

	public async Task<DitadoParaRealizarResponse?> ObterDitadoParaRealizarAsync(int id)
	{
		var ditado = await _context.Ditados
			.Include(d => d.Segmentos)
			.FirstOrDefaultAsync(d => d.Id == id && d.Ativo);

		if (ditado == null)
			return null;

		var audioBase64 = $"data:{Dominio.Entidades.Ditado.AudioMimeTypePadrao};base64,{Convert.ToBase64String(ditado.AudioLeitura)}";

		return new DitadoParaRealizarResponse
		{
			Id = ditado.Id,
			Titulo = ditado.Titulo,
			AudioBase64 = audioBase64,
			Segmentos = ditado.Segmentos.OrderBy(s => s.Ordem).Select(s => new SegmentoParaRealizarDto
			{
				Ordem = s.Ordem,
				Tipo = s.Tipo.ToString(),
				Conteudo = s.Tipo == TipoSegmento.Texto ? s.Conteudo : null,
				SegmentoId = s.Tipo == TipoSegmento.Lacuna ? s.Id : null
			}).ToList()
		};
	}

	public async Task<ResultadoDitadoResponse?> SubmeterRespostaAsync(int ditadoId, SubmeterRespostaRequest request, int alunoId)
	{
		var ditado = await _context.Ditados
			.Include(d => d.Segmentos)
			.FirstOrDefaultAsync(d => d.Id == ditadoId && d.Ativo);

		if (ditado == null)
			return null;

		var respostaDitado = new RespostaDitado
		{
			DitadoId = ditadoId,
			AlunoId = alunoId,
			DataRealizacao = DateTime.UtcNow
		};

		var detalhes = new List<DetalheRespostaDto>();
		int acertos = 0;
		int totalLacunas = 0;

		foreach (var resposta in request.Respostas)
		{
			var segmento = ditado.Segmentos.FirstOrDefault(s => s.Id == resposta.SegmentoId);
			if (segmento == null || segmento.Tipo != TipoSegmento.Lacuna)
				continue;

			totalLacunas++;
			var correto = NormalizarTexto(resposta.Resposta) == NormalizarTexto(segmento.Conteudo);
			if (correto) acertos++;

			var tipoErro = correto ? TipoErro.Nenhum : IdentificarTipoErro(resposta.Resposta, segmento.Conteudo);

			var respostaSegmento = new RespostaSegmento
			{
				SegmentoId = segmento.Id,
				RespostaFornecida = resposta.Resposta,
				Correto = correto,
				TipoErro = correto ? null : tipoErro
			};

			respostaDitado.RespostasSegmentos.Add(respostaSegmento);

			detalhes.Add(new DetalheRespostaDto
			{
				SegmentoId = segmento.Id,
				RespostaFornecida = resposta.Resposta,
				RespostaEsperada = segmento.Conteudo,
				Correto = correto,
				TipoErro = correto ? null : tipoErro.ObterDescricao()
			});
		}

		respostaDitado.Nota = totalLacunas > 0 ? (decimal)acertos / totalLacunas * 100 : 0;

		_context.RespostaDitados.Add(respostaDitado);
		await _context.SaveChangesAsync();

		return new ResultadoDitadoResponse
		{
			RespostaDitadoId = respostaDitado.Id,
			Nota = respostaDitado.Nota,
			TotalLacunas = totalLacunas,
			Acertos = acertos,
			Erros = totalLacunas - acertos,
			Detalhes = detalhes
		};
	}

	public async Task<List<DitadoResponse>> ListarDitadosAsync()
	{
		return await _context.Ditados
			.Include(d => d.DitadoCategorias)
			.ThenInclude(dc => dc.Categoria)
			.Include(d => d.Autor)
			.Where(d => d.Ativo)
			.OrderByDescending(d => d.DataCriacao)
			.Select(d => new DitadoResponse
			{
				Id = d.Id,
				Titulo = d.Titulo,
				Descricao = d.Descricao,
				DataCriacao = d.DataCriacao,
				AutorId = d.AutorId,
				AutorNome = d.Autor != null ? d.Autor.Nome : null, 
				Categorias = d.DitadoCategorias
					.Select(dc => new CategoriaSimplificadaDto
					{
						Id = dc.Categoria.Id,
						Nome = dc.Categoria.Nome
					})
					.ToList()
			})
			.ToListAsync();
	}

	private async Task<DitadoResponse> ObterDitadoComCategoriasAsync(int ditadoId)
	{
		var ditado = await _context.Ditados
			.Include(d => d.DitadoCategorias)
			.ThenInclude(dc => dc.Categoria)
			.Include(d => d.Autor)
			.FirstOrDefaultAsync(d => d.Id == ditadoId);

		if (ditado == null)
			throw new InvalidOperationException("Ditado não encontrado.");

		return new DitadoResponse
		{
			Id = ditado.Id,
			Titulo = ditado.Titulo,
			Descricao = ditado.Descricao,
			DataCriacao = ditado.DataCriacao,
			AutorId = ditado.AutorId,
			AutorNome = ditado.Autor?.Nome,
			Categorias = ditado.DitadoCategorias
				.Select(dc => new CategoriaSimplificadaDto
				{
					Id = dc.Categoria.Id,
					Nome = dc.Categoria.Nome
				})
				.ToList()
		};
	}

	public async Task<DitadoResponse?> AtualizarCategoriasAsync(int ditadoId, List<int> categoriaIds)
	{
		var ditado = await _context.Ditados
			.Include(d => d.DitadoCategorias)
			.FirstOrDefaultAsync(d => d.Id == ditadoId);

		if (ditado == null)
			return null;

		// Validar categorias
		await _categoriaService.ValidarCategoriasAsync(categoriaIds);

		// Remover associações antigas
		_context.DitadoCategorias.RemoveRange(ditado.DitadoCategorias);

		// Adicionar novas associações
		foreach (var categoriaId in categoriaIds.Distinct())
		{
			_context.DitadoCategorias.Add(new DitadoCategoria
			{
				DitadoId = ditado.Id,
				CategoriaId = categoriaId,
				DataAssociacao = DateTime.UtcNow
			});
		}

		await _context.SaveChangesAsync();

		return await ObterDitadoComCategoriasAsync(ditado.Id);
	}

	public async Task<DitadoResponse?> AtualizarDitadoAsync(int id, AtualizarDitadoRequest request)
	{
		var ditado = await _context.Ditados
			.Include(d => d.DitadoCategorias)
			.FirstOrDefaultAsync(d => d.Id == id);

		if (ditado == null)
			return null;

		// Atualizar título se fornecido
		if (!string.IsNullOrWhiteSpace(request.Titulo))
			ditado.Titulo = request.Titulo;

		// Atualizar descrição se fornecido (permite limpar)
		if (request.Descricao != null)
			ditado.Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao;

		// Atualizar status ativo se fornecido
		if (request.Ativo.HasValue)
			ditado.Ativo = request.Ativo.Value;

		// Atualizar categorias se fornecido (permite 0 categorias)
		if (request.CategoriaIds != null)
		{
			// Validar categorias se houver alguma
			if (request.CategoriaIds.Any())
				await _categoriaService.ValidarCategoriasAsync(request.CategoriaIds);

			// Remover associações antigas
			_context.DitadoCategorias.RemoveRange(ditado.DitadoCategorias);

			// Adicionar novas associações (pode ser lista vazia)
			foreach (var categoriaId in request.CategoriaIds.Distinct())
			{
				_context.DitadoCategorias.Add(new DitadoCategoria
				{
					DitadoId = ditado.Id,
					CategoriaId = categoriaId,
					DataAssociacao = DateTime.UtcNow
				});
			}
		}

		await _context.SaveChangesAsync();

		return await ObterDitadoComCategoriasAsync(ditado.Id);
	}

	public async Task<bool> DeletarDitadoAsync(int ditadoId, int usuarioLogadoId, TipoUsuario tipoUsuarioLogado)
	{
		var ditado = await _context.Ditados.FindAsync(ditadoId);

		if (ditado == null)
			return false;

		if (tipoUsuarioLogado != TipoUsuario.Administrador && ditado.AutorId != usuarioLogadoId)
			throw new InvalidOperationException("Apenas o autor do ditado ou um administrador podem excluí-lo.");

		_context.Ditados.Remove(ditado);
		await _context.SaveChangesAsync();

		return true;
	}

	private List<DitadoSegmento> ParsearTextoComLacunas(string texto)
	{
		var segmentos = new List<DitadoSegmento>();
		var regex = new Regex(@"\[([^\]]+)\]");
		int ordem = 1;
		int ultimoIndice = 0;

		foreach (Match match in regex.Matches(texto))
		{
			// Extrai o conteúdo dentro dos colchetes (pode ter espaços/pontuação)
			var conteudoBruto = match.Groups[1].Value;

			// Separa palavra limpa de prefixo/sufixo (espaços e pontuação)
			var (prefixo, palavraLimpa, sufixo) = ExtrairPalavraComContexto(conteudoBruto);

			// Adiciona texto ANTES da lacuna (incluindo o que estava antes dos colchetes + prefixo)
			var textoAntes = texto.Substring(ultimoIndice, match.Index - ultimoIndice) + prefixo;

			if (!string.IsNullOrEmpty(textoAntes))
			{
				segmentos.Add(new DitadoSegmento
				{
					Ordem = ordem++,
					Tipo = TipoSegmento.Texto,
					Conteudo = textoAntes
				});
			}

			// Adiciona a LACUNA com apenas a palavra limpa (preserva maiúscula/minúscula original)
			segmentos.Add(new DitadoSegmento
			{
				Ordem = ordem++,
				Tipo = TipoSegmento.Lacuna,
				Conteudo = palavraLimpa
			});

			// Atualiza índice
			ultimoIndice = match.Index + match.Length;

			// Se tem sufixo, ele será o início do próximo segmento de texto
			if (!string.IsNullOrEmpty(sufixo))
			{
				// Guarda o sufixo para concatenar com o próximo texto
				var proximoTextoInicio = ultimoIndice;
				var proximoTextoFim = ultimoIndice < texto.Length ?
					(regex.Match(texto, ultimoIndice).Success ? regex.Match(texto, ultimoIndice).Index : texto.Length)
					: texto.Length;

				var proximoTexto = proximoTextoInicio < proximoTextoFim ?
					texto.Substring(proximoTextoInicio, proximoTextoFim - proximoTextoInicio) : string.Empty;

				var textoComSufixo = sufixo + proximoTexto;

				if (!string.IsNullOrEmpty(textoComSufixo) && proximoTextoInicio < texto.Length)
				{
					segmentos.Add(new DitadoSegmento
					{
						Ordem = ordem++,
						Tipo = TipoSegmento.Texto,
						Conteudo = textoComSufixo
					});

					// Atualiza índice para pular o texto já processado
					ultimoIndice = proximoTextoFim;
				}
				else if (!string.IsNullOrEmpty(sufixo))
				{
					// Apenas sufixo, sem texto adicional
					segmentos.Add(new DitadoSegmento
					{
						Ordem = ordem++,
						Tipo = TipoSegmento.Texto,
						Conteudo = sufixo
					});
				}
			}
		}

		// Adiciona texto restante após a última lacuna
		if (ultimoIndice < texto.Length)
		{
			segmentos.Add(new DitadoSegmento
			{
				Ordem = ordem,
				Tipo = TipoSegmento.Texto,
				Conteudo = texto.Substring(ultimoIndice)
			});
		}

		return segmentos;
	}

	/// <summary>
	/// Extrai a palavra limpa e separa prefixo/sufixo (espaços e pontuação).
	/// Preserva a capitalização original da palavra.
	/// Exemplo: ", Gato. " > (", ", "Gato", ". ")
	/// </summary>
	private (string prefixo, string palavra, string sufixo) ExtrairPalavraComContexto(string conteudo)
	{
		// Regex que captura: (prefixo) (palavra) (sufixo)
		// Prefixo: espaços e pontuação (incluindo ponto) no início
		// Palavra: letras (com acentos), números, apóstrofo e hífen
		// Sufixo: espaços e pontuação (incluindo ponto) no fim
		var match = Regex.Match(
			conteudo,
			@"^([\s,\.;:!?\-—'""«»]*)([a-zA-ZáàâãäéèêëíìîïóòôõöúùûüçÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇ'\-]+)([\s,\.;:!?\-—'""«»]*)$"
		);

		if (match.Success)
		{
			return (match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
		}

		// Fallback: Se não conseguiu fazer match, tenta pelo menos separar espaços
		var trimmed = conteudo.Trim();
		var prefixoEspacos = conteudo.Substring(0, conteudo.IndexOf(trimmed[0]));
		var sufixoEspacos = conteudo.Substring(conteudo.LastIndexOf(trimmed[^1]) + 1);

		return (prefixoEspacos, trimmed, sufixoEspacos);
	}

	private byte[] ExtrairAudioDeBase64(string audioBase64)
	{
		var base64Data = audioBase64.Contains(",") ? audioBase64.Split(',')[1] : audioBase64;
		return Convert.FromBase64String(base64Data);
	}

	private string NormalizarTexto(string texto)
	{
		// ignora maiúsculas/minúsculas
		return texto.Trim().ToLowerInvariant();
	}

	private TipoErro IdentificarTipoErro(string resposta, string esperado)
	{
		var respostaNorm = NormalizarTexto(resposta);
		var esperadoNorm = NormalizarTexto(esperado);

		// 1. Verifica omissão (resposta vazia ou muito curta)
		if (string.IsNullOrWhiteSpace(respostaNorm))
			return TipoErro.Omissao;

		// 2. Verifica erro de acentuação (se remover acentos ficam iguais)
		if (RemoverAcentos(respostaNorm) == RemoverAcentos(esperadoNorm))
			return TipoErro.Acentuacao;

		// === TESTES PARA ERROS NA PRIMEIRA LETRA ===

		// 3. Omissão de "H" inicial
		// Ex: "omem" em vez de "homem"
		if (esperadoNorm.Length > 0 && esperadoNorm[0] == 'h' && 
			respostaNorm.Length > 0 && respostaNorm[0] != 'h' &&
			esperadoNorm.Substring(1) == respostaNorm)
		{
			return TipoErro.Irregularidade;
		}

		// 4. Acréscimo de "H" inicial
		// Ex: "hontem" em vez de "ontem"
		if (respostaNorm.Length > 0 && respostaNorm[0] == 'h' && 
			esperadoNorm.Length > 0 && esperadoNorm[0] != 'h' &&
			respostaNorm.Substring(1) == esperadoNorm)
		{
			return TipoErro.Irregularidade;
		}

		// 5. Troca S/C iniciais antes de E/I
		// Ex: "cigno" em vez de "signo", "sigarro" em vez de "cigarro"
		if (respostaNorm.Length > 1 && esperadoNorm.Length > 1)
		{
			var primeiraLetraResposta = respostaNorm[0];
			var primeiraLetraEsperado = esperadoNorm[0];
			var segundaLetraResposta = respostaNorm[1];
			var segundaLetraEsperado = esperadoNorm[1];

			// Se primeira letra é S ou C e segunda letra é E ou I
			if ((primeiraLetraResposta == 's' || primeiraLetraResposta == 'c') &&
				(primeiraLetraEsperado == 's' || primeiraLetraEsperado == 'c') &&
				primeiraLetraResposta != primeiraLetraEsperado &&
				(segundaLetraResposta == 'e' || segundaLetraResposta == 'i') &&
				segundaLetraResposta == segundaLetraEsperado &&
				respostaNorm.Substring(1) == esperadoNorm.Substring(1))
			{
				return TipoErro.Contextual;
			}
		}

		// 6. Omitir a primeira letra (SUPRESSÃO)
		// Ex: "este" em vez de "teste"
		if (respostaNorm.Length == esperadoNorm.Length - 1 &&
			esperadoNorm.Substring(1) == respostaNorm)
		{
			return TipoErro.Supressao;
		}

		// 7. Adicionar uma letra antes (ACRÉSCIMO)
		// Ex: "oteste" em vez de "teste"
		if (respostaNorm.Length == esperadoNorm.Length + 1 &&
			respostaNorm.Substring(1) == esperadoNorm)
		{
			return TipoErro.Acrescimo;
		}

		// 8. Trocar a primeira letra (TROCA)
		// Ex: "deste" em vez de "teste"
		if (respostaNorm.Length == esperadoNorm.Length &&
			respostaNorm.Length > 0 &&
			respostaNorm[0] != esperadoNorm[0] &&
			respostaNorm.Substring(1) == esperadoNorm.Substring(1))
		{
			return TipoErro.Troca;
		}

		// === FIM DOS TESTES DE PRIMEIRA LETRA ===

		// 9. Verifica se é erro ortográfico puro (mesmo comprimento, letras diferentes)
		if (respostaNorm.Length == esperadoNorm.Length)
		{
			// Conta quantos caracteres são diferentes
			int diferencas = 0;
			for (int i = 0; i < respostaNorm.Length; i++)
			{
				if (respostaNorm[i] != esperadoNorm[i])
					diferencas++;
			}

			// Se tem diferenças mas mesmo tamanho, é ortográfico (troca de letras)
			if (diferencas > 0)
				return TipoErro.Ortografico;
		}

		// 10. Diferença de comprimento pode ser omissão OU ortográfico
		var diferencaComprimento = Math.Abs(respostaNorm.Length - esperadoNorm.Length);

		if (diferencaComprimento <= 2)
		{
			// Verifica similaridade: quantos caracteres em comum?
			var caracteresComuns = respostaNorm.Intersect(esperadoNorm).Count();
			var tamanhoMenor = Math.Min(respostaNorm.Length, esperadoNorm.Length);

			// Se mais de 70% dos caracteres são comuns, é ortográfico
			if (tamanhoMenor > 0 && (double)caracteresComuns / tamanhoMenor >= 0.7)
				return TipoErro.Ortografico;
		}

		// 11. Grande diferença de comprimento = omissão
		return TipoErro.Omissao;
	}

	private string RemoverAcentos(string texto)
	{
		var acentos = "áàâãäéèêëíìîïóòôõöúùûüçÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇ";
		var semAcentos = "aaaaaeeeeiiiiooooouuuucAAAAAEEEEIIIIOOOOOUUUUC";

		for (int i = 0; i < acentos.Length; i++)
		{
			texto = texto.Replace(acentos[i], semAcentos[i]);
		}

		return texto;
	}
}