using Microsoft.EntityFrameworkCore.Design;

namespace VideoGameLibrary.Data
{
    // Solo la usan las herramientas de EF Core en tiempo de diseño (dotnet ef migrations add/...),
    // que necesitan crear un GameDbContext sin pasar por App.xaml.cs. La cadena de conexión no
    // necesita apuntar a un servidor real, solo hace falta para poder construir el contexto y leer el modelo.
    public class GameDbContextFactory : IDesignTimeDbContextFactory<GameDbContext>
    {
        public GameDbContext CreateDbContext(string[] args) =>
            new GameDbContext("Host=design-time;Database=design-time;Username=design-time;Password=design-time;");
    }
}
