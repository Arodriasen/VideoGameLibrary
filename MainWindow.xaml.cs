using VideoGameLibrary.Services;
using VideoGameLibrary.ViewModels;
using VideoGameLibrary.Views;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VideoGameLibrary
{
    public partial class MainWindow : Window
    {
        private MainViewModel Vm => (MainViewModel)DataContext;

        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            UpdateThemeIcon();
            Loaded += async (_, _) =>
            {
                await vm.LoadGamesAsync();
                TxtQuickScan.Focus();
            };
        }

        private async void TxtQuickScan_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            await Vm.QuickAddByBarcodeAsync();
            TxtQuickScan.Focus();
        }

        private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            App.ToggleTheme();
            UpdateThemeIcon();
        }

        private void UpdateThemeIcon()
        {
            ThemeIcon.Kind = App.IsDarkTheme ? PackIconKind.WeatherSunny : PackIconKind.WeatherNight;
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SettingsDialog { Owner = this };
            dialog.ShowDialog();
        }

        private void BtnLog_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new LogViewerDialog { Owner = this };
            dialog.ShowDialog();
        }

        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var editVm = App.GetEditViewModel();
            var dialog = new GameEditDialog(editVm) { Owner = this };

            if (dialog.ShowDialog() == true)
            {
                var game = editVm.ToGame();
                try
                {
                    await Vm.SaveGameAsync(game);
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
                {
                    LoggingService.LogError("Guardar juego — código de barras duplicado", ex);
                    MessageBox.Show("Ya existe un juego con ese código de barras en la colección.",
                        "Código de barras duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void BtnView_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is not ViewModels.GameViewModel gvm) return;

            var dialog = new GameDetailDialog(gvm) { Owner = this };
            dialog.ShowDialog();
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is not ViewModels.GameViewModel gvm) return;

            var editVm = App.GetEditViewModel();
            editVm.LoadFromGame(gvm.ToModel());
            var dialog = new GameEditDialog(editVm) { Owner = this };

            if (dialog.ShowDialog() == true)
            {
                var game = editVm.ToGame();
                game.Id = gvm.Id;
                await Vm.SaveGameAsync(game);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is not ViewModels.GameViewModel gvm) return;

            var result = MessageBox.Show(
                $"¿Eliminar \"{gvm.Title}\" de la colección?",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await Vm.DeleteGameAsync(gvm);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Eliminar juego \"{gvm.Title}\"", ex);
                MessageBox.Show($"Error al eliminar el juego:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();

            var itemExcel = new MenuItem { Header = "Exportar a Excel (.xlsx)" };
            itemExcel.Click += async (_, _) => await ExportAsync("xlsx");

            var itemCsv = new MenuItem { Header = "Exportar a CSV" };
            itemCsv.Click += async (_, _) => await ExportAsync("csv");

            menu.Items.Add(itemExcel);
            menu.Items.Add(itemCsv);
            menu.PlacementTarget = (Button)sender;
            menu.IsOpen = true;
        }

        private async System.Threading.Tasks.Task ExportAsync(string format)
        {
            var dlg = new SaveFileDialog();

            if (format == "xlsx")
            {
                dlg.Filter = "Excel (*.xlsx)|*.xlsx";
                dlg.FileName = "coleccion.xlsx";
            }
            else
            {
                dlg.Filter = "CSV (*.csv)|*.csv";
                dlg.FileName = "coleccion.csv";
            }

            if (dlg.ShowDialog() != true) return;

            var games = await App.Repository.GetAllAsync();
            var exporter = new Services.ExportService();

            if (format == "xlsx")
                exporter.ExportToExcel(games, dlg.FileName);
            else
                exporter.ExportToCsv(games, dlg.FileName);

            MessageBox.Show($"Exportación completada: {dlg.FileName}",
                "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
