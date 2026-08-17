using VideoGameLibrary.Data;
using VideoGameLibrary.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VideoGameLibrary.Services
{
    public class GameRepository
    {
        private readonly GameDbContext _db;

        public GameRepository(GameDbContext db)
        {
            _db = db;
            _db.Database.EnsureCreated();
            EnsureRatingColumn();
            EnsurePlayedColumn();
        }

        // Añade la columna Rating a bases de datos creadas antes de introducir el sistema de puntuación
        private void EnsureRatingColumn()
        {
            var hasRating = _db.Database.SqlQueryRaw<string>("SELECT name FROM pragma_table_info('Games') WHERE name = 'Rating'").AsEnumerable().Any();
            if (!hasRating)
                _db.Database.ExecuteSqlRaw("ALTER TABLE Games ADD COLUMN Rating INTEGER NOT NULL DEFAULT 0");
        }

        // Añade la columna Played a bases de datos creadas antes de introducir la marca de "jugado"
        private void EnsurePlayedColumn()
        {
            var hasPlayed = _db.Database.SqlQueryRaw<string>("SELECT name FROM pragma_table_info('Games') WHERE name = 'Played'").AsEnumerable().Any();
            if (!hasPlayed)
                _db.Database.ExecuteSqlRaw("ALTER TABLE Games ADD COLUMN Played INTEGER NOT NULL DEFAULT 0");
        }

        public async Task<List<Game>> GetAllAsync()
        {
            return await _db.Games.AsNoTracking().OrderBy(g => g.Title).ToListAsync();
        }

        public async Task<Game?> GetByBarcodeAsync(string barcode)
        {
            if (string.IsNullOrEmpty(barcode)) return null;
            return await _db.Games.FirstOrDefaultAsync(g => g.Barcode == barcode);
        }

        public async Task AddAsync(Game game)
        {
            _db.Games.Add(game);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Game game)
        {
            // El DbContext vive durante toda la sesión de la app, así que una edición anterior
            // del mismo juego puede seguir bajo seguimiento con otra instancia distinta.
            // Hay que soltarla antes de adjuntar la nueva o EF Core lanza un conflicto de clave.
            var tracked = _db.ChangeTracker.Entries<Game>()
                .FirstOrDefault(e => e.Entity.Id == game.Id && e.Entity != game);
            if (tracked != null)
                tracked.State = EntityState.Detached;

            // El diálogo de edición no conoce la fecha de alta original (siempre trae el valor
            // por defecto = ahora); hay que preservarla o cada edición la pisaría con la fecha actual.
            game.AddedDate = await _db.Games.AsNoTracking()
                .Where(g => g.Id == game.Id)
                .Select(g => g.AddedDate)
                .FirstOrDefaultAsync();

            _db.Games.Update(game);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var game = await _db.Games.FindAsync(id);
            if (game != null)
            {
                _db.Games.Remove(game);
                await _db.SaveChangesAsync();
            }
        }

        // Compacta el archivo .db, eliminando físicamente los restos de los registros borrados.
        // Reescribe el archivo entero — se deja como acción manual (ver Ajustes) en vez de automática
        // para que no penalice el rendimiento si la colección crece mucho.
        public void Vacuum()
        {
            _db.Database.ExecuteSqlRaw("VACUUM");
        }
    }
}
