using Microsoft.EntityFrameworkCore;
using Techinal_Exam.Models;

namespace Techinal_Exam.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Documents> Documents => Set<Documents>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Documents>(e =>
            {
                e.ToTable("Documents");
                e.HasIndex(d => d.StoredFileName).IsUnique();
                e.Property(d => d.DateandTime).HasDefaultValueSql("GETUTCDATE()");
            });
        }
    }
}
