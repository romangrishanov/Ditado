using Ditado.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ditado.Infra.Configurations;

public class DitadoCategoriaConfiguration : IEntityTypeConfiguration<DitadoCategoria>
{
    public void Configure(EntityTypeBuilder<DitadoCategoria> builder)
    {
        builder.ToTable("DitadoCategorias");
        
        builder.HasKey(dc => new { dc.DitadoId, dc.CategoriaId });
        
        builder.Property(dc => dc.DataAssociacao)
            .IsRequired();
        
        builder.HasOne(dc => dc.Ditado)
            .WithMany(d => d.DitadoCategorias)
            .HasForeignKey(dc => dc.DitadoId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(dc => dc.Categoria)
            .WithMany(c => c.DitadoCategorias)
            .HasForeignKey(dc => dc.CategoriaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}