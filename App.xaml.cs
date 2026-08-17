using VideoGameLibrary.Data;
using VideoGameLibrary.Services;
using VideoGameLibrary.ViewModels;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace VideoGameLibrary
{
    public partial class App : Application
    {
        public static GameRepository Repository { get; private set; } = null!;
        private static GameApiService _apiService = null!;
        public static GameApiService ApiService => _apiService;
        public static bool IsDarkTheme { get; private set; }
        public static string CurrentDatabasePath { get; private set; } = string.Empty;

        private static readonly string ConfigFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoGameLibrary");
        private static readonly string ConfigFile = Path.Combine(ConfigFolder, "config.json");

        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LoggingService.LogError("Excepción no controlada (hilo de interfaz)", e.Exception);
            MessageBox.Show(
                $"Ha ocurrido un error inesperado:\n\n{e.Exception.Message}\n\nSe ha guardado el detalle en el registro de errores.",
                "Error inesperado", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                LoggingService.LogError("Excepción no controlada (AppDomain)", ex);
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LoggingService.LogError("Excepción no observada en tarea en segundo plano", e.Exception);
            e.SetObserved();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var config = LoadConfig();
                IsDarkTheme = config.DarkTheme;
                ApplyTheme(IsDarkTheme);

                if (string.IsNullOrEmpty(config.ScanDexToken) && string.IsNullOrEmpty(config.IgdbClientId) &&
                    string.IsNullOrEmpty(config.IgdbClientSecret) && string.IsNullOrEmpty(config.RawgApiKey) &&
                    string.IsNullOrEmpty(config.TheGamesDbApiKey))
                {
                    new Views.SettingsDialog(firstRun: true).ShowDialog();
                    config = LoadConfig();
                }

                var dbPath = GetOrSelectDatabase();
                if (dbPath == null)
                {
                    Shutdown();
                    return;
                }

                var db = new GameDbContext(dbPath);
                Repository = new GameRepository(db);
                CurrentDatabasePath = dbPath;
                await Repository.PurgeExpiredTrashAsync();

                _apiService = new GameApiService(
                    config.ScanDexToken,
                    config.IgdbClientId,
                    config.IgdbClientSecret,
                    config.RawgApiKey,
                    config.TheGamesDbApiKey);

                var mainVm = new MainViewModel(Repository, _apiService);
                var mainWindow = new MainWindow(mainVm);
                mainWindow.Show();

                _ = CheckForUpdatesAsync(mainVm);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error al iniciar la aplicación", ex);
                MessageBox.Show(
                    $"Error al iniciar la aplicación:\n\n{ex.Message}\n\n{ex.InnerException?.Message}",
                    "Error de inicio", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private static async Task CheckForUpdatesAsync(MainViewModel mainVm)
        {
            var update = await UpdateCheckService.CheckForUpdateAsync();
            if (update == null) return;

            mainVm.SnackbarMessageQueue.Enqueue(
                $"Hay una nueva versión disponible ({update.Version}).",
                "DESCARGAR",
                () => Process.Start(new ProcessStartInfo(update.Url) { UseShellExecute = true }));
        }

        private static string? GetOrSelectDatabase()
        {
            var lastDb = LoadConfig().LastDatabasePath;

            if (!string.IsNullOrEmpty(lastDb) && File.Exists(lastDb))
                return lastDb;

            return PromptForDatabase();
        }

        // Cambia de colección sin reiniciar la app: crea un repositorio nuevo apuntando al
        // .db elegido y lo deja como el activo. Quien llame es responsable de refrescar la UI.
        public static async Task<bool> SwitchDatabaseInteractiveAsync()
        {
            var path = PromptForDatabase();
            if (path == null) return false;

            await SwitchDatabaseAsync(path);
            return true;
        }

        public static async Task SwitchDatabaseAsync(string path)
        {
            var db = new GameDbContext(path);
            Repository = new GameRepository(db);
            CurrentDatabasePath = path;
            await Repository.PurgeExpiredTrashAsync();
            SaveLastPath(path);
        }

        // Renombra (o mueve) el archivo .db actual. Hay que soltar el archivo antes de moverlo:
        // Dispose() cierra la conexión, pero Microsoft.Data.Sqlite mantiene un pool de conexiones
        // que puede seguir reteniendo el archivo hasta que se limpia explícitamente.
        public static Task<string?> RenameDatabaseFileAsync(string newFileNameOrPath)
        {
            var fileName = newFileNameOrPath.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
                ? newFileNameOrPath
                : newFileNameOrPath + ".db";
            var newPath = Path.IsPathRooted(fileName)
                ? fileName
                : Path.Combine(Path.GetDirectoryName(CurrentDatabasePath)!, fileName);

            if (string.Equals(newPath, CurrentDatabasePath, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<string?>(null);

            if (File.Exists(newPath))
                throw new IOException("Ya existe un archivo con ese nombre.");

            Repository.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Move(CurrentDatabasePath, newPath);

            var db = new GameDbContext(newPath);
            Repository = new GameRepository(db);
            CurrentDatabasePath = newPath;
            SaveLastPath(newPath);

            return Task.FromResult<string?>(newPath);
        }

        private static string? PromptForDatabase()
        {
            var result = MessageBox.Show(
                "¿Quieres abrir una colección existente o crear una nueva?\n\n" +
                "Sí → Abrir existente (.db)\nNo → Crear nueva",
                "Mi Colección de Juegos — Seleccionar base de datos",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel) return null;

            if (result == MessageBoxResult.Yes)
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Abrir colección existente",
                    Filter = "Base de datos (*.db)|*.db",
                    DefaultExt = ".db"
                };
                if (dlg.ShowDialog() != true) return null;
                SaveLastPath(dlg.FileName);
                return dlg.FileName;
            }
            else
            {
                var dlg = new SaveFileDialog
                {
                    Title = "Crear nueva colección",
                    Filter = "Base de datos (*.db)|*.db",
                    DefaultExt = ".db",
                    FileName = "MiColeccionJuegos"
                };
                if (dlg.ShowDialog() != true) return null;
                SaveLastPath(dlg.FileName);
                return dlg.FileName;
            }
        }

        public static AppConfig LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigFile)) return new AppConfig();
                var json = File.ReadAllText(ConfigFile);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch
            {
                return new AppConfig();
            }
        }

        public static void SaveApiKeys(string scanDexToken, string igdbClientId, string igdbClientSecret,
                                        string rawgApiKey, string theGamesDbApiKey)
        {
            var config = LoadConfig();
            config.ScanDexToken = scanDexToken;
            config.IgdbClientId = igdbClientId;
            config.IgdbClientSecret = igdbClientSecret;
            config.RawgApiKey = rawgApiKey;
            config.TheGamesDbApiKey = theGamesDbApiKey;

            Directory.CreateDirectory(ConfigFolder);
            File.WriteAllText(ConfigFile, JsonSerializer.Serialize(config));

            _apiService?.UpdateKeys(scanDexToken, igdbClientId, igdbClientSecret, rawgApiKey, theGamesDbApiKey);
        }

        private static void SaveLastPath(string path)
        {
            try
            {
                var config = LoadConfig();
                config.LastDatabasePath = path;
                Directory.CreateDirectory(ConfigFolder);
                var json = JsonSerializer.Serialize(config);
                File.WriteAllText(ConfigFile, json);
            }
            catch (Exception ex) { LoggingService.LogError("Guardar ruta de la última base de datos", ex); }
        }

        public static GameEditViewModel GetEditViewModel()
            => new GameEditViewModel(_apiService);

        public static void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            ApplyTheme(IsDarkTheme);
            SaveThemePreference(IsDarkTheme);
        }

        private static void ApplyTheme(bool dark)
        {
            var paletteHelper = new PaletteHelper();
            var currentTheme = paletteHelper.GetTheme();
            var newTheme = Theme.Create(
                dark ? BaseTheme.Dark : BaseTheme.Light,
                currentTheme.PrimaryMid.Color,
                currentTheme.SecondaryMid.Color);
            paletteHelper.SetTheme(newTheme);
        }

        private static void SaveThemePreference(bool dark)
        {
            try
            {
                var config = LoadConfig();
                config.DarkTheme = dark;
                Directory.CreateDirectory(ConfigFolder);
                File.WriteAllText(ConfigFile, JsonSerializer.Serialize(config));
            }
            catch (Exception ex) { LoggingService.LogError("Guardar preferencia de tema", ex); }
        }

        public class AppConfig
        {
            public string LastDatabasePath { get; set; } = string.Empty;
            public string ScanDexToken { get; set; } = string.Empty;
            public string IgdbClientId { get; set; } = string.Empty;
            public string IgdbClientSecret { get; set; } = string.Empty;
            public string RawgApiKey { get; set; } = string.Empty;
            public string TheGamesDbApiKey { get; set; } = string.Empty;
            public bool DarkTheme { get; set; }
        }
    }
}
