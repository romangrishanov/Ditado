using Ditado.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ditado.Infra.Configurations;

public class RespostaDitadoConfiguration : IEntityTypeConfiguration<RespostaDitado>
{
    public void Configure(EntityTypeBuilder<RespostaDitado> builder)
    {
        builder.ToTable("RespostasDitados");
        
        builder.HasKey(r => r.Id);
        
        builder.Property(r => r.DataRealizacao)
            .IsRequired();
        
        builder.Property(r => r.Nota)
            .IsRequired()
            .HasPrecision(5, 2);
        
        builder.Property(r => r.AlunoId)
            .IsRequired();
        
        builder.HasOne(r => r.Ditado)
            .WithMany(d => d.Respostas)
            .HasForeignKey(r => r.DitadoId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(r => r.Aluno)
            .WithMany()
            .HasForeignKey(r => r.AlunoId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(r => r.RespostasSegmentos)
            .WithOne(rs => rs.RespostaDitado)
            .HasForeignKey(rs => rs.RespostaDitadoId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Índice para buscar tentativas de um aluno em um ditado
        builder.HasIndex(r => new { r.AlunoId, r.DitadoId, r.DataRealizacao });
    }
}