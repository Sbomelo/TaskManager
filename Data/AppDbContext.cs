using Microsoft.EntityFrameworkCore;
using TaskManager.Models;

namespace TaskManager.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<TaskItem> Tasks => Set<TaskItem>();
        public DbSet<Board> Boards => Set<Board>();
        public DbSet<AppUser> Users => Set<AppUser>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Board>().HasIndex(b => b.Slug).IsUnique();
            modelBuilder.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<TaskItem>().HasIndex(t => t.BoardId);

            modelBuilder.Entity<Board>().HasData(
                new Board { Id = 1, Slug = "engineering", DisplayName = "Engineering", CreatedAt = new DateTime(2026, 1, 1) },
                new Board { Id = 2, Slug = "devops", DisplayName = "DevOps", CreatedAt = new DateTime(2026, 1, 1) },
                new Board { Id = 3, Slug = "design", DisplayName = "Design", CreatedAt = new DateTime(2026, 1, 1) }
            );
        }

    }
}
