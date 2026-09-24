using System;
using System.Threading.Tasks;
using System.Windows;
using MaterialDesignThemes.Wpf;
using VideoGameLibrary.Application.Abstractions;
using VideoGameLibrary.Presentation.Views;

namespace VideoGameLibrary.Presentation.Services
{
    // Envoltorio fino sobre DialogHost para mostrar avisos con el estilo Material de la app en
    // vez del MessageBox.Show nativo de Windows. La Window que llame necesita tener su propio
    // <md:DialogHost Identifier="..."> en el XAML raíz; ese identificador es el que se pasa aquí.
    public class AppDialogService : IAppDialogService
    {
        public Task ShowInfoAsync(string identifier, string message, string title = "Información")
            => Show(identifier, message, title, AppDialogSeverity.Info);

        public Task ShowWarningAsync(string identifier, string message, string title = "Aviso")
            => Show(identifier, message, title, AppDialogSeverity.Warning);

        public Task ShowErrorAsync(string identifier, string message, string title = "Error")
            => Show(identifier, message, title, AppDialogSeverity.Error);

        // Devuelve true solo si se pulsó "SÍ" (ver AppConfirmDialog, CommandParameter "True"/"False").
        public async Task<bool> ShowConfirmAsync(string identifier, string message, string title = "Confirmar")
        {
            try
            {
                var result = await DialogHost.Show(new AppConfirmDialog(message, title), identifier);
                return result is string s && s == "True";
            }
            catch (InvalidOperationException)
            {
                return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            }
        }

        // DialogHost.Show lanza InvalidOperationException si la ventana de ese DialogHost ya se ha
        // cerrado (p. ej. el usuario la cierra mientras se espera una respuesta de red lenta y el
        // aviso llega después) o si ya tiene otro diálogo abierto. En esos casos el mensaje no se
        // pierde ni tumba la app: se muestra con el MessageBox nativo de Windows.
        private static async Task Show(string identifier, string message, string title, AppDialogSeverity severity)
        {
            try
            {
                await DialogHost.Show(new AppMessageDialog(message, title, severity), identifier);
            }
            catch (InvalidOperationException)
            {
                var icon = severity switch
                {
                    AppDialogSeverity.Error => MessageBoxImage.Error,
                    AppDialogSeverity.Warning => MessageBoxImage.Warning,
                    _ => MessageBoxImage.Information
                };
                MessageBox.Show(message, title, MessageBoxButton.OK, icon);
            }
        }
    }
}
