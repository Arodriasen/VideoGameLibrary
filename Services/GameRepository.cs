using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VideoGameLibrary.Data;
using VideoGameLibrary.Models;

namespace VideoGameLibrary.Services
{
    public class GameRepository : IDisposable
    {
        // Días que un juego permanece en la papelera antes de borrarse para siempre.
        // Usado tanto por PurgeExpiredTrashAsync como por la UI de la papelera (cuenta atrás por juego).
        public const int TrashRetentionDays = 7;

        private readonly GameDbContext _db;

        public GameRepository(GameDbContext db)
        {
            _db = db;
            MigrateDatabase();
            EnsureCollectionSettingsTable();
        }

        public void Dispose() => _db.Dispose();

        // Aplica las migraciones de EF Core. La base de datos en Neon nace vacía (no hay
        // instalaciones antiguas que preservar), así que basta con Migrate() sin bootstrap especial.
        private void MigrateDatabase() => _db.Database.Migrate();

        // Tabla de una sola fila con el nombre visible de la colección. Los identificadores van
        // entre comillas dobles porque Postgres pliega a minúsculas lo que no va entre comillas,
        // y así coinciden con el PascalCase que usa EF Core para el resto de tablas/columnas.
        private void EnsureCollectionSettingsTable()
        {
            _db.Database.ExecuteSqlRaw(
                "CREATE TABLE IF NOT EXISTS \"CollectionSettings\" (\"Id\" integer PRIMARY KEY CHECK (\"Id\" = 1), \"Name\" text NOT NULL DEFAULT '')");
        }

        public async Task<string> GetCollectionNameAsync()
        {
            var rows = await _db.Database.SqlQueryRaw<string>("SELECT \"Name\" FROM \"CollectionSettings\" WHERE \"Id\" = 1").ToListAsync();
            return rows.FirstOrDefault() ?? string.Empty;
        }

        public async Task SetCollectionNameAsync(string name)
        {
            await _db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"CollectionSettings\" (\"Id\", \"Name\") VALUES (1, {0}) " +
                "ON CONFLICT (\"Id\") DO UPDATE SET \"Name\" = EXCLUDED.\"Name\"", name);
        }

        public async Task<List<Game>> GetAllAsync()
        {
            return await _db.Games.AsNoTracking().Where(g => g.DeletedDate == null).OrderBy(g => g.Title).ToListAsync();
        }

        public async Task<Game?> GetByBarcodeAsync(string barcode)
        {
            if (string.IsNullOrEmpty(barcode)) return null;
            return await _db.Games.FirstOrDefaultAsync(g => g.Barcode == barcode && g.DeletedDate == null);
        }

        public async Task AddAsync(Game game)
        {
            _db.Games.Add(game);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // El _db vive durante toda la sesión de la app (ver comentario en MigrateDatabase):
                // si no se suelta aquí, la entidad fallida se queda en Added para siempre y revienta
                // el SIGUIENTE SaveChangesAsync de cualquier operación, aunque no tenga nada que ver
                // (por eso un "código duplicado" al añadir podía bloquear luego un borrado normal).
                _db.Entry(game).State = EntityState.Detached;
                throw;
            }
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

        // Borrado suave: el juego pasa a la papelera (ver GetTrashAsync) en vez de borrarse
        // de verdad, para poder recuperarlo. PurgeExpiredTrashAsync limpia lo antiguo.
        public async Task DeleteAsync(int id)
        {
            var game = await _db.Games.FindAsync(id);
            if (game != null)
            {
                game.DeletedDate = DateTime.Now;
                await _db.SaveChangesAsync();
            }
        }

        public async Task RestoreAsync(int id)
        {
            var game = await _db.Games.FindAsync(id);
            if (game != null)
            {
                game.DeletedDate = null;
                await _db.SaveChangesAsync();
            }
        }

        public async Task<List<Game>> GetTrashAsync()
        {
            return await _db.Games.AsNoTracking()
                .Where(g => g.DeletedDate != null)
                .OrderByDescending(g => g.DeletedDate)
                .ToListAsync();
        }

        // Borrado real, sin paso por la papelera — usado por "Eliminar definitivamente",
        // "Vaciar papelera" y por la limpieza automática de la papelera caducada.
        public async Task PermanentlyDeleteAsync(int id)
        {
            var game = await _db.Games.FindAsync(id);
            if (game != null)
            {
                _db.Games.Remove(game);
                await _db.SaveChangesAsync();
            }
        }

        // Se llama al arrancar la app: borra de verdad lo que lleva más de retentionDays en la papelera
        public async Task<int> PurgeExpiredTrashAsync(int retentionDays = TrashRetentionDays)
        {
            var cutoff = DateTime.Now.AddDays(-retentionDays);
            var expired = await _db.Games.Where(g => g.DeletedDate != null && g.DeletedDate < cutoff).ToListAsync();
            if (expired.Count == 0) return 0;

            _db.Games.RemoveRange(expired);
            await _db.SaveChangesAsync();
            return expired.Count;
        }

        // Inserta varios juegos importados de golpe. Cada uno se guarda por separado para que
        // un código de barras duplicado no descarte el resto del lote; la entidad fallida se
        // suelta del seguimiento del contexto (si no, EF reintentaría guardarla en cada fila siguiente).
        public async Task<(int Added, int Duplicates)> ImportAsync(IEnumerable<Game> games)
        {
            int added = 0, duplicates = 0;

            foreach (var game in games)
            {
                _db.Games.Add(game);
                try
                {
                    await _db.SaveChangesAsync();
                    added++;
                }
                catch (DbUpdateException)
                {
                    _db.Entry(game).State = EntityState.Detached;
                    duplicates++;
                }
            }

            return (added, duplicates);
        }
    }
}
