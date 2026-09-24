using System;

namespace VideoGameLibrary.Application.Abstractions
{
    // Errores del servicio de cuentas ya traducidos (ver SupabaseAccountService), para que la
    // interfaz pueda mostrar un mensaje claro a cada caso sin conocer la librería de Supabase.

    // El servicio no responde a tiempo, no hay conexión o falla por su lado: reintentar más tarde
    public class AccountServiceUnavailableException : Exception
    {
        public AccountServiceUnavailableException(Exception inner)
            : base("El servicio de cuentas no está disponible en este momento.", inner) { }
    }

    public class InvalidCredentialsException : Exception
    {
        public InvalidCredentialsException(Exception inner)
            : base("Correo o contraseña incorrectos.", inner) { }
    }

    public class EmailNotConfirmedException : Exception
    {
        public EmailNotConfirmedException(Exception inner)
            : base("La cuenta todavía no está confirmada.", inner) { }
    }
}
