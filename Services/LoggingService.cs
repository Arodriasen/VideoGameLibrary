using System;
using System.IO;
using System.Linq;
using System.Text;

namespace VideoGameLibrary.Services
{
    public static class LoggingService
    {
        private static readonly string LogFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoGameLibrary", "logs");
        private static readonly object _lock = new object();
        private const string Separator = "--------------------------------------------------------------------------------";

        public static string LogFolderPath => LogFolder;

        public static void LogError(string context, Exception ex)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(LogFolder);
                    var file = Path.Combine(LogFolder, $"errors-{DateTime.Now:yyyy-MM-dd}.log");
                    var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}{ex}{Environment.NewLine}{Separator}{Environment.NewLine}";
                    File.AppendAllText(file, entry, Encoding.UTF8);
                }
            }
            catch
            {
                // El propio registro de errores nunca debe tirar la app abajo
            }
        }

        // Contenido de todos los días de log concatenado, del más antiguo al más reciente
        public static string ReadAllLogsText()
        {
            try
            {
                if (!Directory.Exists(LogFolder)) return string.Empty;

                var files = Directory.GetFiles(LogFolder, "errors-*.log").OrderBy(f => f);

                var sb = new StringBuilder();
                foreach (var file in files)
                    sb.Append(File.ReadAllText(file, Encoding.UTF8));

                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void ClearLogs()
        {
            try
            {
                if (!Directory.Exists(LogFolder)) return;
                foreach (var file in Directory.GetFiles(LogFolder, "errors-*.log"))
                    File.Delete(file);
            }
            catch
            {
            }
        }
    }
}
