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

            // Garante que o banco é criado
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DitadoDbContext>();
            context.Database.EnsureCreated();
        });
    }
}