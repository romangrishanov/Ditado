using Microsoft.EntityFrameworkCore.Migrations;
using System.Security.Cryptography;
using System.Text;


#nullable disable

namespace Ditado.Infra.Migrations
{
    /// <inheritdoc />
    public partial class CriarUsuarioAdminInicialTemp : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Gera hash da senha "admin" usando PBKDF2
			var senhaHash = GerarHashSenha("admin");

			migrationBuilder.InsertData(
				table: "Usuarios",
				columns: new[] { "Nome", "Login", "SenhaHash", "Tipo", "Ativo", "DataCriacao", "DataUltimoAcesso" },
				values: new object[]
				{
					"Administrador Temporário",
					"admin@admin.com",
					senhaHash,
					1, // TipoUsuario.Administrador
                    true,
					DateTime.UtcNow,
					null
				}
			);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql("DELETE FROM Usuarios WHERE Login = 'admin@admin.com'");
		}

		// Método auxiliar para gerar hash (replica lógica do PasswordHasher)
		private string GerarHashSenha(string senha)
		{
			const int SaltSize = 16;
			const int KeySize = 32;
			const int Iterations = 100000;
			var Algorithm = HashAlgorithmName.SHA256;

			var salt = RandomNumberGenerator.GetBytes(SaltSize);
			var hash = Rfc2898DeriveBytes.Pbkdf2(
				Encoding.UTF8.GetBytes(senha),
				salt,
				Iterations,
				Algorithm,
				KeySize
			);

			return $"{Convert.ToHexString(salt)}-{Convert.ToHexString(hash)}";
		}
	}
}
