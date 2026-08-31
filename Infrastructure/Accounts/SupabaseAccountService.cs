using System;
using System.Linq;
using System.Threading.Tasks;
using Supabase;
using Supabase.Gotrue;
using VideoGameLibrary.Application.Abstractions;
using VideoGameLibrary.Application.Models;
using VideoGameLibrary.Infrastructure.Logging;

namespace VideoGameLibrary.Infrastructure.Accounts
{
    public class SupabaseAccountService : IAccountService
    {
        private readonly Supabase.Client _client;

        public SupabaseAccountService(string url, string anonKey)
        {
            var options = new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = false,
                SessionHandler = new FileSessionPersistence()
            };
            _client = new Supabase.Client(url, anonKey, options);
        }

        public Task InitializeAsync() => _client.InitializeAsync();

        public async Task<bool> TryRestoreSessionAsync()
        {
            try
            {
                var session = await _client.Auth.RetrieveSessionAsync();
                return session != null;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Restaurar sesión de cuenta", ex);
                return false;
            }
        }

        // Con la confirmación de email activada en el proyecto Supabase (comportamiento por
        // defecto), SignUp no deja una sesión activa hasta que el usuario confirma desde el
        // correo -- de ahí devolver ConfirmationRequired en vez de asumir que ya hay sesión.
        public async Task<SignUpResult> SignUpAsync(string email, string password)
        {
            var session = await _client.Auth.SignUp(email, password);
            return string.IsNullOrEmpty(session?.AccessToken) ? SignUpResult.ConfirmationRequired : SignUpResult.SignedIn;
        }

        public async Task SignInAsync(string email, string password)
        {
            await _client.Auth.SignInWithPassword(email, password);
        }

        // Local (no Global): cierra sesión solo en este dispositivo, no revoca la sesión de los
        // demás dispositivos donde el usuario también haya iniciado sesión.
        public async Task SignOutAsync()
        {
            await _client.Auth.SignOut(Constants.SignOutScope.Local);
        }

        public async Task RequestPasswordResetAsync(string email)
        {
            await _client.Auth.ResetPasswordForEmail(email);
        }

        public async Task ResetPasswordAsync(string email, string code, string newPassword)
        {
            // VerifyOTP deja una sesión de recuperación activa, que es la que permite el
            // siguiente Update (cambiar la contraseña sin conocer la anterior).
            await _client.Auth.VerifyOTP(email, code, Constants.EmailOtpType.Recovery);
            await _client.Auth.Update(new UserAttributes { Password = newPassword });
        }

        public async Task<UserSettings?> GetSettingsAsync()
        {
            try
            {
                var response = await _client.From<UserSettingsRow>().Get();
                var row = response.Models.FirstOrDefault();
                if (row == null) return null;

                return new UserSettings
                {
                    ConnectionString = row.ConnectionString,
                    ScanDexToken = row.ScanDexToken,
                    IgdbClientId = row.IgdbClientId,
                    IgdbClientSecret = row.IgdbClientSecret,
                    RawgApiKey = row.RawgApiKey,
                    TheGamesDbApiKey = row.TheGamesDbApiKey
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Recuperar ajustes de la cuenta", ex);
                return null;
            }
        }

        public async Task SaveSettingsAsync(UserSettings settings)
        {
            var userId = _client.Auth.CurrentUser?.Id
                ?? throw new InvalidOperationException("No hay ninguna sesión activa.");

            var row = new UserSettingsRow
            {
                UserId = Guid.Parse(userId),
                ConnectionString = settings.ConnectionString,
                ScanDexToken = settings.ScanDexToken,
                IgdbClientId = settings.IgdbClientId,
                IgdbClientSecret = settings.IgdbClientSecret,
                RawgApiKey = settings.RawgApiKey,
                TheGamesDbApiKey = settings.TheGamesDbApiKey
            };

            // Upsert (no Insert): la primera vez crea la fila, las siguientes la actualizan --
            // el conflicto se resuelve por la clave primaria (user_id), que coincide siempre con
            // el usuario autenticado gracias a RLS.
            await _client.From<UserSettingsRow>().Upsert(row);
        }
    }
}
