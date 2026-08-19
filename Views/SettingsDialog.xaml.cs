using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using VideoGameLibrary.Services;
using VideoGameLibrary.ViewModels;

namespace VideoGameLibrary.Views
{
    public partial class SettingsDialog : Window
    {
        public SettingsDialog(bool firstRun = false)
        {
            InitializeComponent();

            var config = App.LoadConfig();
            TxtConnectionString.Text = config.ConnectionString;
            TxtScanDex.Text = config.ScanDexToken;
            TxtIgdbClientId.Text = config.IgdbClientId;
            TxtIgdbClientSecret.Text = config.IgdbClientSecret;
            TxtRawg.Text = config.RawgApiKey;
            TxtGamesDb.Text = config.TheGamesDbApiKey;

            if (firstRun)
            {
                TxtIntro.Text = "Bienvenido a Mi Colección de Juegos. Antes de empezar, introduce la cadena de conexión de tu " +
                                 "base de datos en Neon. También puedes añadir tus claves de API (opcionales) para que el " +
                                 "escaneo de códigos de barras encuentre título, portada y datos automáticamente.";
                BtnCancel.Content = "CANCELAR";

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
                await AppDialogService.ShowErrorAsync("SettingsDialogHost", $"No se ha podido guardar el nombre:\n{ex.Message}");
            }
        }

        private async void BtnSaveConnection_Click(object sender, RoutedEventArgs e)
        {
            var connectionString = TxtConnectionString.Text.Trim();
            if (connectionString.Length == 0)
            {
                await AppDialogService.ShowWarningAsync("SettingsDialogHost", "Introduce una cadena de conexión.", "Conexión a la base de datos");
                return;
            }

            try
            {
                await App.ReconnectAsync(connectionString);
                // No cierra el diálogo: en el primer arranque el usuario puede seguir rellenando
                // las claves de API y guardar con el botón GUARDAR de abajo. MainWindow detecta el
                // cambio de repositorio comparando referencias cuando el diálogo se cierre (ver BtnSettings_Click).
                await AppDialogService.ShowInfoAsync("SettingsDialogHost", "Conexión establecida correctamente.", "Conexión a la base de datos");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Conectar con la base de datos Neon", ex);
                await AppDialogService.ShowErrorAsync("SettingsDialogHost",
                    $"No se ha podido conectar con la base de datos. Revisa la cadena de conexión y tu conexión a internet:\n{ex.Message}");
            }
        }

        private async void BtnFindDuplicates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var all = await App.Repository.GetAllAsync();
                var groups = ImportService.FindDuplicateGroups(all);

                if (groups.Count == 0)
                {
                    await AppDialogService.ShowInfoAsync("SettingsDialogHost",
                        "No se han encontrado posibles duplicados en tu colección.", "Buscar duplicados");
                    return;
                }

                var dlg = new DuplicatesDialog(groups) { Owner = this };
                if (dlg.ShowDialog() != true || dlg.SelectedIds.Count == 0) return;

                foreach (var id in dlg.SelectedIds)
                    await App.Repository.DeleteAsync(id);

                await AppDialogService.ShowInfoAsync("SettingsDialogHost",
                    $"{dlg.SelectedIds.Count} juego(s) enviados a la papelera. Puedes restaurarlos desde ahí si te equivocaste.",
                    "Buscar duplicados");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Buscar duplicados en la colección", ex);
                await AppDialogService.ShowErrorAsync("SettingsDialogHost", $"No se ha podido completar la búsqueda:\n{ex.Message}");
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
                var importer = new ImportService();
                var headers = importer.ReadHeaders(dlg.FileName);
                if (headers.Count == 0)
                {
                    await AppDialogService.ShowWarningAsync("SettingsDialogHost",
                        "No se ha podido leer ninguna cabecera de columna en el archivo.", "Importar colección");
                    return;
                }

                var guessed = ImportService.GuessMapping(headers);
                var mappingDlg = new ImportColumnMappingDialog(headers, guessed) { Owner = this };
                if (mappingDlg.ShowDialog() != true) return;

                var parsed = importer.ParseFile(dlg.FileName, mappingDlg.Mapping);

                if (parsed.Count == 0)
                {
                    await AppDialogService.ShowWarningAsync("SettingsDialogHost",
                        "No se ha encontrado ninguna fila válida. Revisa que el archivo tenga una fila de cabecera y una columna \"Título\".",
                        "Importar colección");
                    return;
                }

                var existing = await App.Repository.GetAllAsync();
                var classified = ImportService.BuildPreview(parsed, existing);
                var previewItems = classified.Select(c => new ImportPreviewItem(c.Game, c.Status)).ToList();

                var previewDlg = new ImportPreviewDialog(previewItems) { Owner = this };
                if (previewDlg.ShowDialog() != true) return;

                if (previewDlg.SelectedGames.Count == 0)
                {
                    await AppDialogService.ShowInfoAsync("SettingsDialogHost",
                        "No se ha seleccionado ningún juego para importar.", "Importar colección");
                    return;
                }

                var (added, duplicates) = await App.Repository.ImportAsync(previewDlg.SelectedGames);

                var msg = $"Importación completada.\n\nAñadidos: {added}";
                if (duplicates > 0) msg += $"\nOmitidos por conflicto en la base de datos: {duplicates}";

                await AppDialogService.ShowInfoAsync("SettingsDialogHost", msg, "Importar colección");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Importar colección desde CSV/Excel", ex);
                await AppDialogService.ShowErrorAsync("SettingsDialogHost", $"No se ha podido importar el archivo:\n{ex.Message}");
            }
        }
    }
}
