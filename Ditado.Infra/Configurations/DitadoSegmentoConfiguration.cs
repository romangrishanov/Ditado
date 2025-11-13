using Ditado.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ditado.Infra.Configurations;

public class DitadoSegmentoConfiguration : IEntityTypeConfiguration<DitadoSegmento>
{
    public void Configure(EntityTypeBuilder<DitadoSegmento> builder)
    {
        builder.ToTable("DitadoSegmentos");
        
        builder.HasKey(s => s.Id);
        
        builder.Property(s => s.Ordem)
            .IsRequired();
        
        builder.Property(s => s.Tipo)
            .IsRequired()
            .HasConversion<int>();
        
        builder.Property(s => s.Conteudo)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.HasIndex(s => new { s.DitadoId, s.Ordem })
            .IsUnique();
    }
}