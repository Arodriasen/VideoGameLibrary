using System;
using System.Threading.Tasks;
using System.Windows;
using VideoGameLibrary.Application.Abstractions;
using VideoGameLibrary.Application.Models;
using VideoGameLibrary.Infrastructure.Logging;

namespace VideoGameLibrary.Presentation.Views
{
    public partial class LoginWindow : Window
    {
        private readonly IAccountService _accountService;

        public LoginWindow(IAccountService accountService)
        {
            InitializeComponent();
            _accountService = accountService;

            var remembered = App.LoadRememberedLogin();
            if (remembered != null)
            {
                TxtEmail.Text = remembered.Email;
                TxtPassword.Password = remembered.Password;
            }
        }

        private async void BtnSignIn_Click(object sender, RoutedEventArgs e)
        {
            var email = TxtEmail.Text.Trim();
            var password = TxtPassword.Password;

            if (email.Length == 0 || password.Length == 0)
            {
                await App.DialogService.ShowWarningAsync("LoginDialogHost", "Introduce tu correo y contraseña.");
                return;
            }

            SetBusy(true);
            try
            {
                await _accountService.SignInAsync(email, password);
                await ResolveSettingsAndCloseAsync(email, password);
            }
            catch (InvalidCredentialsException)
            {
                // Error del usuario, no de la app: no se registra
                await App.DialogService.ShowErrorAsync("LoginDialogHost",
                    "Correo o contraseña incorrectos. Si no la recuerdas, usa \"¿Olvidaste tu contraseña?\".",
                    "Imposible iniciar sesión");
            }
            catch (EmailNotConfirmedException)
            {
                await App.DialogService.ShowWarningAsync("LoginDialogHost",
                    "Tu cuenta todavía no está confirmada. Revisa tu correo (también la carpeta de spam) y pulsa el enlace de confirmación.",
                    "Imposible iniciar sesión");
            }
            catch (Exception ex)
            {
                // Servicio caído, sin conexión, sin respuesta a tiempo o cualquier otro fallo: el
                // detalle técnico va al registro de errores, el usuario solo necesita saber que reintente
                LoggingService.LogError("Iniciar sesión", ex);
                await App.DialogService.ShowErrorAsync("LoginDialogHost",
                    "Imposible iniciar sesión, reinténtelo más tarde.", "Imposible iniciar sesión");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void BtnSignUp_Click(object sender, RoutedEventArgs e)
        {
            var email = TxtEmail.Text.Trim();
            var password = TxtPassword.Password;

            if (email.Length == 0 || password.Length == 0)
            {
                await App.DialogService.ShowWarningAsync("LoginDialogHost", "Introduce tu correo y contraseña.");
                return;
            }

            SetBusy(true);
            try
            {
                var result = await _accountService.SignUpAsync(email, password);
                if (result == SignUpResult.ConfirmationRequired)
                {
                    await App.DialogService.ShowInfoAsync("LoginDialogHost",
                        "Cuenta creada. Revisa tu correo para confirmarla y luego inicia sesión aquí.",
                        "Confirma tu correo");
                    return;
                }

                await ResolveSettingsAndCloseAsync(email, password);
            }
            catch (AccountServiceUnavailableException ex)
            {
                LoggingService.LogError("Crear cuenta", ex);
                await App.DialogService.ShowErrorAsync("LoginDialogHost",
                    "Imposible crear la cuenta, reinténtelo más tarde.", "Imposible crear la cuenta");
            }
            catch (Exception ex)
            {
                // Aquí el mensaje de Supabase sí es útil (p. ej. "ya existe una cuenta con ese correo")
                LoggingService.LogError("Crear cuenta", ex);
                await App.DialogService.ShowErrorAsync("LoginDialogHost", $"No se ha podido crear la cuenta:\n{ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        // Tras iniciar sesión o crear cuenta con éxito: si la cuenta ya tiene ajustes guardados no
        // hace falta nada más (App.xaml.cs los recoge al continuar el arranque). Si está vacía
        // (cuenta nueva) y este equipo ya tenía una configuración local, se ofrece subirla una vez.
        private async Task ResolveSettingsAndCloseAsync(string email, string password)
        {
            if (ChkRemember.IsChecked == true)
                App.SaveRememberedLogin(email, password);
            else
                App.ClearRememberedLogin();

            UserSettings? remote;
            try
            {
                remote = await _accountService.GetSettingsAsync();
            }
            catch (Exception ex)
            {
                // No se sabe qué tiene la cuenta: NO se ofrece subir la configuración local (podría
                // pisar la de la cuenta). Se sigue con la de este equipo; App.OnStartup vuelve a
                // intentar leer la de la cuenta justo después.
                LoggingService.LogError("Recuperar ajustes de la cuenta tras iniciar sesión", ex);
                DialogResult = true;
                Close();
                return;
            }

            if (remote == null || string.IsNullOrEmpty(remote.ConnectionString))
            {
                var local = App.LoadConfig();
                if (!string.IsNullOrEmpty(local.ConnectionString))
                {
                    var upload = await App.DialogService.ShowConfirmAsync("LoginDialogHost",
                        "Este equipo ya tiene una configuración guardada (cadena de conexión y claves de API). " +
                        "¿Quieres subirla a tu cuenta para tenerla disponible en tus demás dispositivos?",
                        "Sincronizar configuración");

                    if (upload)
                    {
                        try
                        {
                            await _accountService.SaveSettingsAsync(new UserSettings
                            {
                                ConnectionString = local.ConnectionString,
                                ScanDexToken = local.ScanDexToken,
                                IgdbClientId = local.IgdbClientId,
                                IgdbClientSecret = local.IgdbClientSecret,
                                RawgApiKey = local.RawgApiKey,
                                TheGamesDbApiKey = local.TheGamesDbApiKey
                            });
                        }
                        catch (Exception ex)
                        {
                            LoggingService.LogError("Subir configuración local a la cuenta", ex);
                            await App.DialogService.ShowErrorAsync("LoginDialogHost",
                                $"No se ha podido subir la configuración a tu cuenta:\n{ex.Message}");
                        }
                    }
                }
            }

            DialogResult = true;
            Close();
        }

        private void TxtForgotPassword_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dlg = new ForgotPasswordWindow(_accountService, TxtEmail.Text.Trim()) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                // Contraseña cambiada: se refleja en el campo para que solo falte pulsar INICIAR SESIÓN.
                TxtPassword.Password = dlg.NewPassword;
            }
        }

        private void SetBusy(bool busy)
        {
            LoadingIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            BtnSignIn.IsEnabled = !busy;
            BtnSignUp.IsEnabled = !busy;
        }
    }
}
