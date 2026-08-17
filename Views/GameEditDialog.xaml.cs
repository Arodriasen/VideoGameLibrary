using VideoGameLibrary.ViewModels;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace VideoGameLibrary.Views
{
    public partial class GameEditDialog : Window
    {
        private GameEditViewModel Vm => (GameEditViewModel)DataContext;

        public GameEditDialog(GameEditViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseDialog = () => DialogResult = vm.Saved;
            Loaded += (_, _) => TxtBarcode.Focus();
        }

        private void TxtBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Vm.SearchCommand.Execute(null);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnChangeCover_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
                Title = "Seleccionar portada"
            };
            if (dlg.ShowDialog() == true)
            {
                Vm.CoverData = File.ReadAllBytes(dlg.FileName);
            }
        }
    }
}
