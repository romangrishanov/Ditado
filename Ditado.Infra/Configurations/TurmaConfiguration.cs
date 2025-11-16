using Ditado.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ditado.Infra.Configurations;

public class TurmaConfiguration : IEntityTypeConfiguration<Turma>
{
    public void Configure(EntityTypeBuilder<Turma> builder)
    {
        builder.ToTable("Turmas");
        
        builder.HasKey(t => t.Id);
        
        builder.Property(t => t.Nome)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(t => t.Serie)
            .IsRequired();
        
        builder.Property(t => t.AnoLetivo)
            .IsRequired();
        
        builder.Property(t => t.Semestre)
            .HasMaxLength(16);
        
        builder.Property(t => t.Descricao)
            .HasMaxLength(500);
        
        builder.Property(t => t.Ativo)
            .IsRequired();
        
        builder.Property(t => t.DataCriacao)
            .IsRequired();

        // Relacionamento: Turma -> Professor Responsável
        builder.HasOne(t => t.ProfessorResponsavel)
            .WithMany(u => u.TurmasComoProfessor)
            .HasForeignKey(t => t.ProfessorResponsavelId)
            .OnDelete(DeleteBehavior.Restrict); // Não permite deletar professor se tiver turma

        // Relacionamento: Turma -> Alunos (N:N)
        builder.HasMany(t => t.Alunos)
            .WithMany(u => u.TurmasComoAluno)
            .UsingEntity<Dictionary<string, object>>(
                "TurmaAluno",
                j => j.HasOne<Usuario>().WithMany().HasForeignKey("AlunoId").OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<Turma>().WithMany().HasForeignKey("TurmaId").OnDelete(DeleteBehavior.Cascade)
            );
    }
}