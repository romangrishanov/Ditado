using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ditado.Infra.Configurations;

public class DitadoConfiguration : IEntityTypeConfiguration<Dominio.Entidades.Ditado>
{
    public void Configure(EntityTypeBuilder<Dominio.Entidades.Ditado> builder)
    {
        builder.ToTable("Ditados");
        
        builder.HasKey(d => d.Id);
        
        builder.Property(d => d.Titulo)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(d => d.Descricao)
            .HasMaxLength(1000);
        
        builder.Property(d => d.AudioLeitura)
            .IsRequired()
            .HasColumnType("MEDIUMBLOB");
        
        builder.Property(d => d.DataCriacao)
            .IsRequired();
        
        builder.Property(d => d.Ativo)
            .IsRequired();
        
        builder.Property(d => d.AutorId)
            .IsRequired(false);
        
        builder.HasOne(d => d.Autor)
            .WithMany()
            .HasForeignKey(d => d.AutorId)
            .OnDelete(DeleteBehavior.SetNull);
        
        builder.HasMany(d => d.Segmentos)
            .WithOne(s => s.Ditado)
            .HasForeignKey(s => s.DitadoId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(d => d.Respostas)
            .WithOne(r => r.Ditado)
            .HasForeignKey(r => r.DitadoId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(d => d.DitadoCategorias)
            .WithOne(dc => dc.Ditado)
            .HasForeignKey(dc => dc.DitadoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}