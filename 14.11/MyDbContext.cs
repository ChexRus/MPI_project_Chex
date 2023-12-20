using Microsoft.EntityFrameworkCore;

public class MyDbContext : DbContext
{
    public DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Настройка подключения к базе данных
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=base;Trusted_Connection=True;");
    }
}
