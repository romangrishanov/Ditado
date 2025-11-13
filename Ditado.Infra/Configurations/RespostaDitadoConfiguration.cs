using Ditado.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ditado.Infra.Configurations;

public class RespostaDitadoConfiguration : IEntityTypeConfiguration<RespostaDitado>
{
    public void Configure(EntityTypeBuilder<RespostaDitado> builder)
    {
        builder.ToTable("RespostaDitados");
        
        builder.HasKey(r => r.Id);
        
        builder.Property(r => r.DataRealizacao)
            .IsRequired();
        
        builder.Property(r => r.Pontuacao)
            .IsRequired()
            .HasPrecision(5, 2);
        
        builder.HasMany(r => r.RespostasSegmentos)
            .WithOne(rs => rs.RespostaDitado)
            .HasForeignKey(rs => rs.RespostaDitadoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}