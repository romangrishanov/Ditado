using Ditado.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ditado.Infra.Configurations;

public class RespostaSegmentoConfiguration : IEntityTypeConfiguration<RespostaSegmento>
{
    public void Configure(EntityTypeBuilder<RespostaSegmento> builder)
    {
        builder.ToTable("RespostaSegmentos");
        
        builder.HasKey(rs => rs.Id);
        
        builder.Property(rs => rs.RespostaFornecida)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(rs => rs.Correto)
            .IsRequired();
        
        builder.Property(rs => rs.TipoErro)
            .HasConversion<int?>();
        
        builder.HasOne(rs => rs.Segmento)
            .WithMany(s => s.Respostas)
            .HasForeignKey(rs => rs.SegmentoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}