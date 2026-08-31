using System;
using System.IO;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using VideoGameLibrary.Infrastructure.Logging;

namespace VideoGameLibrary.Infrastructure.Accounts
{
    // Persiste la sesión de Supabase entre reinicios de la app -- mismo cifrado (DPAPI, ligado
    // al usuario de Windows actual) que App ya usa para config.json. Los métodos de
    // IGotrueSessionPersistence son síncronos por contrato del SDK (ver wiki "Desktop Clients"
    // de supabase-csharp), no hace falta que esta clase haga I/O asíncrono.
    public class FileSessionPersistence : IGotrueSessionPersistence<Session>
    {
        private static readonly string SessionFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoGameLibrary");
        private static readonly string SessionFile = Path.Combine(SessionFolder, "session.dat");

        public void SaveSession(Session session)
        {
            try
            {
                Directory.CreateDirectory(SessionFolder);
                var json = JsonConvert.SerializeObject(session);
                File.WriteAllText(SessionFile, App.Protect(json));
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Guardar sesión de cuenta", ex);
            }
        }

        public void DestroySession()
        {
            try
            {
                if (File.Exists(SessionFile)) File.Delete(SessionFile);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Borrar sesión de cuenta", ex);
            }
        }

        public Session? LoadSession()
        {
            try
            {
                if (!File.Exists(SessionFile)) return null;
                var json = App.Unprotect(File.ReadAllText(SessionFile));
                return JsonConvert.DeserializeObject<Session>(json);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Cargar sesión de cuenta", ex);
                return null;
            }
        }
    }
}
