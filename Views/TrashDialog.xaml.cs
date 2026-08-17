using VideoGameLibrary.Models;
using VideoGameLibrary.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace VideoGameLibrary.Views
{
    public partial class TrashDialog : Window
    {
        // Se marca cuando algo cambia (restaurar / eliminar) para que MainWindow sepa si debe recargar
        public bool Changed { get; private set; }

        public TrashDialog()
        {
            InitializeComponent();
            Loaded += async (_, _) => await ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            var trash = await App.Repository.GetTrashAsync();
            ItemsList.ItemsSource = trash;
            TxtEmpty.Visibility = trash.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            BtnEmptyTrash.IsEnabled = trash.Count > 0;
        }

        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is not Game game) return;

            try
            {
                await App.Repository.RestoreAsync(game.Id);
                Changed = true;
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Restaurar juego de la papelera \"{game.Title}\"", ex);
                MessageBox.Show($"No se ha podido restaurar el juego:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDeleteForever_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is not Game game) return;

            var result = MessageBox.Show(
                $"¿Eliminar \"{game.Title}\" definitivamente? Esta acción no se puede deshacer.",
                "Eliminar definitivamente", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await App.Repository.PermanentlyDeleteAsync(game.Id);
                Changed = true;
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Eliminar definitivamente \"{game.Title}\"", ex);
                MessageBox.Show($"No se ha podido eliminar el juego:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnEmptyTrash_Click(object sender, RoutedEventArgs e)
        {
            var trash = await App.Repository.GetTrashAsync();
            if (trash.Count == 0) return;

            var result = MessageBox.Show(
                $"¿Vaciar la papelera? Se eliminarán definitivamente {trash.Count} juego(s). Esta acción no se puede deshacer.",
                "Vaciar papelera", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                foreach (var game in trash)
                    await App.Repository.PermanentlyDeleteAsync(game.Id);

                Changed = true;
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Vaciar papelera", ex);
                MessageBox.Show($"No se ha podido vaciar la papelera:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
