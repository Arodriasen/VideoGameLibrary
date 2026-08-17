using VideoGameLibrary.ViewModels;
using System.Windows;

namespace VideoGameLibrary.Views
{
    public partial class GameDetailDialog : Window
    {
        public GameDetailDialog(GameViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
