using Ditado.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Ditado.Infra.Data;

public class DitadoDbContext : DbContext
{
    public DitadoDbContext(DbContextOptions<DitadoDbContext> options) : base(options)
    {
    }

    public DbSet<Dominio.Entidades.Ditado> Ditados { get; set; }
    public DbSet<DitadoSegmento> DitadoSegmentos { get; set; }
    public DbSet<RespostaDitado> RespostaDitados { get; set; }
    public DbSet<RespostaSegmento> RespostaSegmentos { get; set; }
    public DbSet<Usuario> Usuarios { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DitadoDbContext).Assembly);
    }
}