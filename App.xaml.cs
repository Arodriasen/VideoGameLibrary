using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using VideoGameLibrary.Data;
using VideoGameLibrary.Services;
using VideoGameLibrary.ViewModels;

namespace VideoGameLibrary
{
    public partial class App : Application
    {
        public static GameRepository Repository { get; private set; } = null!;
        private static GameApiService _apiService = null!;
        public static GameApiService ApiService => _apiService;
        public static bool IsDarkTheme { get; private set; }

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
                LoggingService.PurgeOldLogs();

                var config = LoadConfig();
                IsDarkTheme = config.DarkTheme;
                ApplyTheme(IsDarkTheme);

                if (string.IsNullOrEmpty(config.ConnectionString))
                {
                    // El propio diálogo de primer arranque valida la conexión llamando a
                    // ReconnectAsync (así el usuario ve enseguida si la cadena es incorrecta,
                    // en vez de descubrirlo en un error genérico al arrancar). Si cancela o la
                    // conexión falla, Repository se queda sin asignar y se cierra la app.
                    new Views.SettingsDialog(firstRun: true).ShowDialog();

                    if (Repository == null)
                    {
                        Shutdown();
                        return;
                    }
                }
                else
                {
                    var db = new GameDbContext(config.ConnectionString);
                    Repository = new GameRepository(db);
                    await Repository.PurgeExpiredTrashAsync();
                }

                _apiService = new GameApiService(
                    config.ScanDexToken,
                    config.IgdbClientId,
                    config.IgdbClientSecret,
                    config.RawgApiKey,
                    config.TheGamesDbApiKey);

                var mainVm = new MainViewModel(Repository, _apiService);
                var mainWindow = new MainWindow(mainVm);

                // App.xaml usa ShutdownMode="OnExplicitShutdown" para que cerrar el diálogo de
                // Ajustes del primer arranque (única ventana abierta en ese momento) no cierre la
                // app entera antes de llegar aquí (era exactamente lo que pasaba con el valor por
                // defecto OnLastWindowClose). A partir de aquí, MainWindow pasa a comportarse como
                // siempre: cerrarla cierra la app.
                MainWindow = mainWindow;
                ShutdownMode = ShutdownMode.OnMainWindowClose;

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

        // Guarda una cadena de conexión nueva y reconecta sin reiniciar la app: crea un
        // repositorio nuevo apuntando a la base indicada y lo deja como el activo (aplica
        // migraciones si hace falta). Quien llame es responsable de refrescar la UI.
        public static async Task ReconnectAsync(string connectionString)
        {
            var db = new GameDbContext(connectionString);
            var repo = new GameRepository(db);
            await repo.PurgeExpiredTrashAsync();

            Repository?.Dispose();
            Repository = repo;

            SaveConnectionString(connectionString);
        }

        public static AppConfig LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigFile)) return new AppConfig();
                var json = File.ReadAllText(ConfigFile);
                var config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();

                // Las claves y la cadena de conexión se guardan cifradas (ver PersistConfig); aquí
                // se descifran para que el resto de la app siga trabajando con texto plano en memoria.
                config.ConnectionString = Unprotect(config.ConnectionString);
                config.ScanDexToken = Unprotect(config.ScanDexToken);
                config.IgdbClientId = Unprotect(config.IgdbClientId);
                config.IgdbClientSecret = Unprotect(config.IgdbClientSecret);
                config.RawgApiKey = Unprotect(config.RawgApiKey);
                config.TheGamesDbApiKey = Unprotect(config.TheGamesDbApiKey);
                return config;
            }
            catch
            {
                return new AppConfig();
            }
        }

        // Único punto de escritura de config.json: cifra las claves de API con DPAPI
        // (ligado al usuario de Windows actual) antes de guardar. Las instalaciones que
        // vengan de una versión anterior tenían las claves en texto plano en el archivo;
        // Unprotect las detecta como no cifradas, las deja pasar tal cual, y al llamar aquí
        // de nuevo (el siguiente guardado, del tipo que sea) quedan cifradas sin más pasos.
        private static void PersistConfig(AppConfig config)
        {
            var toStore = new AppConfig
            {
                ConnectionString = Protect(config.ConnectionString),
                ScanDexToken = Protect(config.ScanDexToken),
                IgdbClientId = Protect(config.IgdbClientId),
                IgdbClientSecret = Protect(config.IgdbClientSecret),
                RawgApiKey = Protect(config.RawgApiKey),
                TheGamesDbApiKey = Protect(config.TheGamesDbApiKey),
                DarkTheme = config.DarkTheme
            };

            Directory.CreateDirectory(ConfigFolder);
            File.WriteAllText(ConfigFile, JsonSerializer.Serialize(toStore));
        }

        internal static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        // Si el valor no es un blob DPAPI válido (p.ej. una clave en texto plano guardada por
        // una versión anterior de la app), se devuelve tal cual en vez de fallar.
        internal static string Unprotect(string storedValue)
        {
            if (string.IsNullOrEmpty(storedValue)) return string.Empty;
            try
            {
                var decrypted = ProtectedData.Unprotect(Convert.FromBase64String(storedValue), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return storedValue;
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

            PersistConfig(config);

            _apiService?.UpdateKeys(scanDexToken, igdbClientId, igdbClientSecret, rawgApiKey, theGamesDbApiKey);
        }

        private static void SaveConnectionString(string connectionString)
        {
            var config = LoadConfig();
            config.ConnectionString = connectionString;
            PersistConfig(config);
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
                PersistConfig(config);
            }
            catch (Exception ex) { LoggingService.LogError("Guardar preferencia de tema", ex); }
        }

        public class AppConfig
        {
            public string ConnectionString { get; set; } = string.Empty;
            public string ScanDexToken { get; set; } = string.Empty;
            public string IgdbClientId { get; set; } = string.Empty;
            public string IgdbClientSecret { get; set; } = string.Empty;
            public string RawgApiKey { get; set; } = string.Empty;
            public string TheGamesDbApiKey { get; set; } = string.Empty;
            public bool DarkTheme { get; set; }
        }
    }
}
