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
        // Sin esto, una petición a Supabase que se queda colgada espera los 100 s por defecto de
        // HttpClient con la app aparentemente bloqueada (pasó al iniciar sesión el 2026-09-24).
        // Todo lo que hace este servicio son peticiones pequeñas: 15 s es margen de sobra.
        private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(15);

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

        // WaitAsync no cancela la petición de fondo (el cliente de Supabase no acepta
        // CancellationToken en estas llamadas), pero deja de esperarla y da un error claro.
        private static async Task WithTimeout(Task task)
        {
            try { await task.WaitAsync(NetworkTimeout); }
            catch (TimeoutException) { throw ServiceNotResponding(); }
        }

        private static async Task<T> WithTimeout<T>(Task<T> task)
        {
            try { return await task.WaitAsync(NetworkTimeout); }
            catch (TimeoutException) { throw ServiceNotResponding(); }
        }

        private static TimeoutException ServiceNotResponding() =>
            new($"El servicio de cuentas no ha respondido en {NetworkTimeout.TotalSeconds:0} segundos. Revisa tu conexión a internet e inténtalo de nuevo.");

        // Carga la sesión guardada en este equipo (y la refresca si hace falta). Si no responde a
        // tiempo no se bloquea el arranque: TryRestoreSessionAsync decidirá si hay que pedir login.
        public async Task InitializeAsync()
        {
            try
            {
                await WithTimeout(_client.InitializeAsync());
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Inicializar el servicio de cuentas", ex);
            }
        }

        public async Task<bool> TryRestoreSessionAsync()
        {
            try
            {
                var session = await WithTimeout(_client.Auth.RetrieveSessionAsync());
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
            var session = await WithTimeout(_client.Auth.SignUp(email, password));
            return string.IsNullOrEmpty(session?.AccessToken) ? SignUpResult.ConfirmationRequired : SignUpResult.SignedIn;
        }

        public async Task SignInAsync(string email, string password)
        {
            await WithTimeout(_client.Auth.SignInWithPassword(email, password));
        }

        // Local (no Global): cierra sesión solo en este dispositivo, no revoca la sesión de los
        // demás dispositivos donde el usuario también haya iniciado sesión.
        public async Task SignOutAsync()
        {
            await WithTimeout(_client.Auth.SignOut(Constants.SignOutScope.Local));
        }

        public async Task RequestPasswordResetAsync(string email)
        {
            await WithTimeout(_client.Auth.ResetPasswordForEmail(email));
        }

        public async Task ResetPasswordAsync(string email, string code, string newPassword)
        {
            // VerifyOTP deja una sesión de recuperación activa, que es la que permite el
            // siguiente Update (cambiar la contraseña sin conocer la anterior).
            await WithTimeout(_client.Auth.VerifyOTP(email, code, Constants.EmailOtpType.Recovery));
            await WithTimeout(_client.Auth.Update(new UserAttributes { Password = newPassword }));
        }

        // Los fallos NO se tragan aquí (ver IAccountService): el llamador distingue "no se ha
        // podido consultar" de "la cuenta no tiene ajustes".
        public async Task<UserSettings?> GetSettingsAsync()
        {
            var response = await WithTimeout(_client.From<UserSettingsRow>().Get());
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
            await WithTimeout(_client.From<UserSettingsRow>().Upsert(row));
        }
    }
}
