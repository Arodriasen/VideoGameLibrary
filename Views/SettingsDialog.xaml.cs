using VideoGameLibrary.Services;
using System;
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
                MaintenanceSeparator.Visibility = Visibility.Collapsed;
                MaintenanceSection.Visibility = Visibility.Collapsed;
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
    }
}
