using Ditado.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ditado.Infra.Configurations;

public class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("Categorias");
        
        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.Nome)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(c => c.DataCriacao)
            .IsRequired();
        
        builder.HasIndex(c => c.Nome)
            .IsUnique();
    }
}