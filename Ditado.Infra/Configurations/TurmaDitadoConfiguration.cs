using Ditado.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ditado.Infra.Configurations;

public class TurmaDitadoConfiguration : IEntityTypeConfiguration<TurmaDitado>
{
    public void Configure(EntityTypeBuilder<TurmaDitado> builder)
    {
        builder.ToTable("TurmaDitados");
        
        // Chave composta (TurmaId + DitadoId) - ÚNICA
        builder.HasKey(td => new { td.TurmaId, td.DitadoId });
        
        builder.Property(td => td.DataAtribuicao)
            .IsRequired();
        
        builder.Property(td => td.DataLimite)
            .IsRequired();
        
        builder.HasOne(td => td.Turma)
            .WithMany(t => t.TurmaDitados)
            .HasForeignKey(td => td.TurmaId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(td => td.Ditado)
            .WithMany(d => d.TurmaDitados)
            .HasForeignKey(td => td.DitadoId)
            .OnDelete(DeleteBehavior.Restrict); // Não deleta ditado se tiver atribuído
    }
}