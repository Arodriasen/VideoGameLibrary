using Microsoft.EntityFrameworkCore;
using VideoGameLibrary.Domain.Entities;

namespace VideoGameLibrary.Infrastructure.Persistence
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
            // Índice único FILTRADO: solo cuenta entre juegos activos ("DeletedDate" IS NULL).
            // Sin el filtro, un juego borrado (borrado suave -- sigue en la fila, solo pasa a la
            // papelera) seguía "ocupando" su código de barras, y volver a escanearlo antes de
            // vaciar la papelera o de que caduque (7 días) hacía fallar el guardado con un
            // DbUpdateException de violación de índice único, aunque GetByBarcodeAsync (el aviso
            // de "ya lo tienes") sí ignora la papelera -- las dos comprobaciones no eran
            // consistentes entre sí. Aparte de este filtro, Postgres (a diferencia de SQL Server,
            // igual que SQLite) ya permite varias filas NULL en un índice único normal, así que
            // no hacía falta ningún filtro adicional para los juegos sin Barcode.
            modelBuilder.Entity<Game>()
                .HasIndex(g => g.Barcode)
                .IsUnique()
                .HasFilter("\"DeletedDate\" IS NULL");

            // Por defecto, Npgsql mapea DateTime a "timestamp with time zone" y exige Kind=Utc.
            // La app usa DateTime.Now (hora local) para estos dos campos, igual que hacía con
            // SQLite -- se mapean a "timestamp without time zone" para conservar ese comportamiento
            // sin tener que tocar la lógica de negocio (AddedDate/DeletedDate en GameRepository).
            modelBuilder.Entity<Game>().Property(g => g.AddedDate).HasColumnType("timestamp without time zone");
            modelBuilder.Entity<Game>().Property(g => g.DeletedDate).HasColumnType("timestamp without time zone");
        }
    }
}
