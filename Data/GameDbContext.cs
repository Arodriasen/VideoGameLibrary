using Microsoft.EntityFrameworkCore;
using VideoGameLibrary.Models;

namespace VideoGameLibrary.Data
{
    public class GameDbContext : DbContext
    {
        private readonly string _connectionString;

        public GameDbContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DbSet<Game> Games { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // EnableRetryOnFailure: Neon (como Azure SQL) puede tener fallos transitorios de
            // conexión mientras "despierta" el cómputo tras un periodo de inactividad.
            options.UseNpgsql(_connectionString, o => o.EnableRetryOnFailure());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // A diferencia de SQL Server, Postgres (como SQLite) permite varias filas NULL en un
            // índice único normal, así que no hace falta HasFilter aquí para los juegos sin Barcode.
            modelBuilder.Entity<Game>()
                .HasIndex(g => g.Barcode)
                .IsUnique();

            // Por defecto, Npgsql mapea DateTime a "timestamp with time zone" y exige Kind=Utc.
            // La app usa DateTime.Now (hora local) para estos dos campos, igual que hacía con
            // SQLite -- se mapean a "timestamp without time zone" para conservar ese comportamiento
            // sin tener que tocar la lógica de negocio (AddedDate/DeletedDate en GameRepository).
            modelBuilder.Entity<Game>().Property(g => g.AddedDate).HasColumnType("timestamp without time zone");
            modelBuilder.Entity<Game>().Property(g => g.DeletedDate).HasColumnType("timestamp without time zone");
        }
    }
}
