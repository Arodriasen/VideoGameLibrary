using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoGameLibrary.Domain.Entities;

namespace VideoGameLibrary.Domain.Repositories
{
    public interface IGameRepository : IDisposable
    {
        // Días que un juego permanece en la papelera antes de borrarse para siempre.
        // Usado tanto por PurgeExpiredTrashAsync como por la UI de la papelera (cuenta atrás por juego).
        public const int TrashRetentionDays = 7;

        Task<string> GetCollectionNameAsync();
        Task SetCollectionNameAsync(string name);

        Task<List<Game>> GetAllAsync();

        // Las portadas son lo único pesado de leer (en Neon, con la base "dormida", tardan casi un
        // minuto en total frente a medio segundo del resto): la lista principal se carga sin ellas
        // y las portadas llegan después, por lotes. GetAllAsync sigue trayéndolo todo para quien
        // de verdad las necesita (exportar a Excel, duplicados...).
        Task<List<Game>> GetAllWithoutCoversAsync();
        Task<HashSet<int>> GetIdsWithCoverAsync();
        Task<Dictionary<int, byte[]>> GetCoversAsync(IReadOnlyCollection<int> ids);

        Task<Game?> GetByBarcodeAsync(string barcode);
        Task AddAsync(Game game);
        Task UpdateAsync(Game game);
        Task DeleteAsync(int id);
        Task RestoreAsync(int id);
        Task<List<Game>> GetTrashAsync();
        Task PermanentlyDeleteAsync(int id);
        Task<int> PurgeExpiredTrashAsync(int retentionDays = TrashRetentionDays);
        Task<(int Added, int Duplicates)> ImportAsync(IEnumerable<Game> games);
    }
}
