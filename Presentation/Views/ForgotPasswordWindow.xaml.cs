using System;
using System.Windows;
using VideoGameLibrary.Application.Abstractions;
using VideoGameLibrary.Infrastructure.Logging;

namespace VideoGameLibrary.Presentation.Views
{
    // Recuperación por código (no por enlace web): el enlace de "restablecer contraseña" que
    // envía Supabase por defecto está pensado para que lo abra un navegador y le redirija de
    // vuelta a una web propia -- una app de escritorio no puede capturar esa redirección. Se
    // usa en su lugar el código de 6 dígitos que Supabase también puede enviar en el mismo
    // correo si la plantilla de email incluye {{ .Token }} (hay que añadirlo a mano una vez en
    // el panel de Supabase, Authentication > Email Templates > Reset Password).
    public partial class ForgotPasswordWindow : Window
    {
        private readonly IAccountService _accountService;

        public string NewPassword { get; private set; } = string.Empty;

        public ForgotPasswordWindow(IAccountService accountService, string email)
        {
            InitializeComponent();
            _accountService = accountService;
            TxtEmail.Text = email;
        }

        private async void BtnSendCode_Click(object sender, RoutedEventArgs e)
        {
            var email = TxtEmail.Text.Trim();
            if (email.Length == 0)
            {
                await App.DialogService.ShowWarningAsync("ForgotPasswordDialogHost", "Introduce tu correo electrónico.");
                return;
            }

            SetBusy(true);
            try
            {
                await _accountService.RequestPasswordResetAsync(email);
                StepRequestCode.Visibility = Visibility.Collapsed;
                StepResetPassword.Visibility = Visibility.Visible;
                await App.DialogService.ShowInfoAsync("ForgotPasswordDialogHost",
                    "Si la cuenta existe, te hemos enviado un código por correo. Si no lo ves en unos minutos, revisa también la carpeta de spam.",
                    "Código enviado");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Solicitar código de recuperación", ex);
                await App.DialogService.ShowErrorAsync("ForgotPasswordDialogHost", $"No se ha podido enviar el código:\n{ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void BtnResetPassword_Click(object sender, RoutedEventArgs e)
        {
            var code = TxtCode.Text.Trim();
            var newPassword = TxtNewPassword.Password;
            var confirmPassword = TxtConfirmPassword.Password;

            if (code.Length == 0 || newPassword.Length == 0)
            {
                await App.DialogService.ShowWarningAsync("ForgotPasswordDialogHost", "Introduce el código y la contraseña nueva.");
                return;
            }

            if (newPassword != confirmPassword)
            {
                await App.DialogService.ShowWarningAsync("ForgotPasswordDialogHost", "Las dos contraseñas no coinciden.");
                return;
            }

            SetBusy(true);
            try
            {
                await _accountService.ResetPasswordAsync(TxtEmail.Text.Trim(), code, newPassword);
                NewPassword = newPassword;

                await App.DialogService.ShowInfoAsync("ForgotPasswordDialogHost",
                    "Contraseña cambiada. Ya puedes iniciar sesión con ella.", "Contraseña actualizada");

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Restablecer contraseña", ex);
                await App.DialogService.ShowErrorAsync("ForgotPasswordDialogHost",
                    $"No se ha podido cambiar la contraseña. Revisa el código:\n{ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            LoadingIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            BtnSendCode.IsEnabled = !busy;
            BtnResetPassword.IsEnabled = !busy;
        }
    }
}
