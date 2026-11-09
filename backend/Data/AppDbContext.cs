using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Data.Entities;

namespace SchoolAdmission.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ClassEntity> Classes => Set<ClassEntity>();
    public DbSet<HoldEntity> Holds => Set<HoldEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ClassEntity>(e =>
        {
            e.ToTable("classes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.TotalSeats).IsRequired();
            e.Property(x => x.CreatedAt).IsRequired();
            e.HasIndex(x => x.Name).IsUnique(); // case sensitivity handled in service; DB unique on exact
        });

        modelBuilder.Entity<HoldEntity>(e =>
        {
            e.ToTable("holds");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.ClassId).HasMaxLength(64).IsRequired();
            e.Property(x => x.StudentId).HasMaxLength(17).IsRequired();
            e.Property(x => x.Status).HasMaxLength(16).IsRequired();
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.ExpiresAt).IsRequired();
            e.HasIndex(x => new { x.ClassId, x.Status });
            e.HasIndex(x => x.StudentId);
            e.HasIndex(x => new { x.ClassId, x.StudentId })
                .HasFilter("\"Status\" IN ('HELD','CONFIRMED')")
                .IsUnique();
            e.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId);
        });
    }
}