using System.Threading.Tasks;
using VideoGameLibrary.Application.Models;

namespace VideoGameLibrary.Application.Abstractions
{
    public enum SignUpResult { SignedIn, ConfirmationRequired }

    public interface IAccountService
    {
        Task InitializeAsync();

        // Intenta recuperar una sesión ya guardada en este equipo (ver IGotrueSessionPersistence
        // en Infrastructure) — true si hay sesión válida (o refrescada) sin pedir credenciales.
        Task<bool> TryRestoreSessionAsync();

        Task<SignUpResult> SignUpAsync(string email, string password);
        Task SignInAsync(string email, string password);
        Task SignOutAsync();

        // Envía el correo de recuperación con un código (ver plantilla de email en Supabase,
        // necesita incluir {{ .Token }} -- un enlace web no sirve para una app de escritorio).
        Task RequestPasswordResetAsync(string email);

        // Verifica el código recibido por correo y, si es válido, establece la contraseña nueva.
        Task ResetPasswordAsync(string email, string code, string newPassword);

        // null SOLO si la cuenta no tiene ninguna fila guardada todavía (cuenta nueva). Si la
        // petición falla (sin conexión, Supabase no responde...) lanza una excepción: no hay que
        // confundir "no sé qué ajustes tiene la cuenta" con "la cuenta está vacía", porque lo
        // segundo lleva a ofrecer subir la configuración local y pisar la de la cuenta.
        Task<UserSettings?> GetSettingsAsync();
        Task SaveSettingsAsync(UserSettings settings);
    }
}
