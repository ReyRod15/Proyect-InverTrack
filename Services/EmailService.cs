using System.Threading.Tasks;
using System.Windows;

namespace InverTrack.Services
{
    public class EmailService
    {
        // Simula el envío de un correo mostrando el contenido en un MessageBox.
        // Para uso real, aquí iría la integración con SMTP o alguna API de correo.
        public Task EnviarCodigoAsync(string destinatario, string asunto, string mensaje)
        {
            // MODO DEMO: solo mostrar el código en pantalla para pruebas.
            MessageBox.Show($"Se enviaría un correo a {destinatario} con el siguiente contenido:\n\nAsunto: {asunto}\n\n{mensaje}",
                            "Correo de verificación (demo)", MessageBoxButton.OK, MessageBoxImage.Information);

            return Task.CompletedTask;
        }
    }
}
