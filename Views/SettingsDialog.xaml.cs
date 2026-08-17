using VideoGameLibrary.Services;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;

namespace VideoGameLibrary.Views
{
    public partial class SettingsDialog : Window
    {
        public SettingsDialog(bool firstRun = false)
        {
            InitializeComponent();

            var config = App.LoadConfig();
            TxtScanDex.Text = config.ScanDexToken;
            TxtIgdbClientId.Text = config.IgdbClientId;
            TxtIgdbClientSecret.Text = config.IgdbClientSecret;
            TxtRawg.Text = config.RawgApiKey;
            TxtGamesDb.Text = config.TheGamesDbApiKey;

            if (firstRun)
            {
                TxtIntro.Text = "Bienvenido a Mi Colección de Juegos. Antes de empezar, puedes introducir tus claves de API " +
                                 "para que el escaneo de códigos de barras encuentre título, portada y datos automáticamente. " +
                                 "Son opcionales: puedes dejarlas en blanco y añadirlas más tarde desde Ajustes.";
                BtnCancel.Content = "OMITIR POR AHORA";

                // Aún no hay ninguna base de datos abierta en este punto del arranque
                SwitchDbSeparator.Visibility = Visibility.Collapsed;
                SwitchDbSection.Visibility = Visibility.Collapsed;
                MaintenanceSeparator.Visibility = Visibility.Collapsed;
                MaintenanceSection.Visibility = Visibility.Collapsed;
                ImportSeparator.Visibility = Visibility.Collapsed;
                ImportSection.Visibility = Visibility.Collapsed;
            }
            else
            {
                Loaded += async (_, _) => TxtCollectionName.Text = await App.Repository.GetCollectionNameAsync();
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            App.SaveApiKeys(
                TxtScanDex.Text.Trim(),
                TxtIgdbClientId.Text.Trim(),
                TxtIgdbClientSecret.Text.Trim(),
                TxtRawg.Text.Trim(),
                TxtGamesDb.Text.Trim());

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        private async void BtnSaveCollectionName_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await App.Repository.SetCollectionNameAsync(TxtCollectionName.Text.Trim());
                DialogResult = true; // cierra Ajustes; MainWindow recarga y refresca el título
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Guardar nombre de la colección", ex);
                MessageBox.Show($"No se ha podido guardar el nombre:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnRenameDbFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Renombrar archivo de la colección",
                Filter = "Base de datos (*.db)|*.db",
                DefaultExt = ".db",
                FileName = Path.GetFileNameWithoutExtension(App.CurrentDatabasePath),
                InitialDirectory = Path.GetDirectoryName(App.CurrentDatabasePath)
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var newPath = await App.RenameDatabaseFileAsync(dlg.FileName);
                if (newPath != null)
                {
                    MessageBox.Show($"Archivo renombrado a:\n{newPath}",
                        "Renombrar archivo", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true; // cierra Ajustes; MainWindow detecta el cambio de repositorio y recarga
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Renombrar archivo .db", ex);
                MessageBox.Show($"No se ha podido renombrar el archivo:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnSwitchDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (await App.SwitchDatabaseInteractiveAsync())
                    DialogResult = true; // cierra Ajustes; MainWindow detecta el cambio y recarga
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Cambiar de colección", ex);
                MessageBox.Show($"No se ha podido abrir esa colección:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnVacuum_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Repository.Vacuum();
                MessageBox.Show("Base de datos compactada correctamente.",
                    "Compactar base de datos", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Compactar base de datos (VACUUM)", ex);
                MessageBox.Show($"No se ha podido compactar la base de datos:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Importar colección",
                Filter = "CSV o Excel (*.csv;*.xlsx)|*.csv;*.xlsx"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var games = new ImportService().ParseFile(dlg.FileName);

                if (games.Count == 0)
                {
                    MessageBox.Show(
                        "No se ha encontrado ninguna fila válida. Revisa que el archivo tenga una fila de cabecera y una columna \"Título\".",
                        "Importar colección", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var (added, duplicates) = await App.Repository.ImportAsync(games);

                MessageBox.Show(
                    $"Importación completada.\n\nAñadidos: {added}\nOmitidos por código de barras duplicado: {duplicates}",
                    "Importar colección", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Importar colección desde CSV/Excel", ex);
                MessageBox.Show($"No se ha podido importar el archivo:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
