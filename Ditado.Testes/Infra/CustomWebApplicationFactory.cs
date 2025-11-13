using System.Security.Cryptography;
using System.Text;
using Ditado.Dominio.Entidades;
using Ditado.Dominio.Enums;
using Ditado.Infra.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ditado.Testes.Infra;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"ditado_test_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove todos os serviços relacionados ao DitadoDbContext e DbContextOptions
            var descriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<DitadoDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(DitadoDbContext) ||
                    (d.ImplementationType != null && (
                        d.ImplementationType.FullName!.Contains("MySql") ||
                        d.ImplementationType.FullName.Contains("Pomelo")
                    ))
                )
                .ToList();

            foreach (var descriptor in descriptors)
                services.Remove(descriptor);

            // Remove todos os DbContextOptions genéricos
            var genericDescriptors = services
                .Where(d => d.ServiceType.FullName != null && d.ServiceType.FullName.Contains("DbContextOptions"))
                .ToList();

            foreach (var descriptor in genericDescriptors)
                services.Remove(descriptor);

            // Adiciona apenas InMemory
            services.AddDbContext<DitadoDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            // Cria banco e faz SEED do admin temporário
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DitadoDbContext>();
            
            context.Database.EnsureCreated();
            
            // Seed do usuário admin temporário
            SeedAdminTemporario(context);
        });
    }

    private void SeedAdminTemporario(DitadoDbContext context)
    {
        // Verifica se já existe admin
        if (context.Usuarios.Any(u => u.Login == "admin@admin.com"))
            return;

        // Gera hash da senha "admin" (mesmo algoritmo do PasswordHasher)
        var senhaHash = GerarHashSenha("admin");

        var adminTemp = new Usuario
        {
            Nome = "Administrador Temporário",
            Login = "admin@admin.com",
            SenhaHash = senhaHash,
            Tipo = TipoUsuario.Administrador,
            Ativo = true,
            DataCriacao = DateTime.UtcNow
        };

        context.Usuarios.Add(adminTemp);
        context.SaveChanges();
    }

    // Replica lógica do PasswordHasher
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