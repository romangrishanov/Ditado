using Ditado.Dominio.Entidades;
using Ditado.Infra.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Ditado.Infra.Data;

public class DitadoDbContext : DbContext
{
    public DitadoDbContext(DbContextOptions<DitadoDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Dominio.Entidades.Ditado> Ditados { get; set; }
    public DbSet<DitadoSegmento> DitadoSegmentos { get; set; }
    public DbSet<RespostaDitado> RespostaDitados { get; set; }
    public DbSet<RespostaSegmento> RespostaSegmentos { get; set; }
    public DbSet<Turma> Turmas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new UsuarioConfiguration());
        modelBuilder.ApplyConfiguration(new DitadoConfiguration());
        modelBuilder.ApplyConfiguration(new DitadoSegmentoConfiguration());
        modelBuilder.ApplyConfiguration(new RespostaDitadoConfiguration());
        modelBuilder.ApplyConfiguration(new RespostaSegmentoConfiguration());
        modelBuilder.ApplyConfiguration(new TurmaConfiguration());
    }
}