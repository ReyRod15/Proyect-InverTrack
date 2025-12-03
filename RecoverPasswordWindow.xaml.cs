using System.Windows;
using InverTrack.Models;
using InverTrack.Services;
using InverTrack.Utils;

namespace InverTrack
{
    // [4] Ventana para recuperar la contraseÃ±a mediante cÃ³digo enviado al correo.
    public partial class RecoverPasswordWindow : Window
    {
        private readonly StorageService _servicioAlmacenamiento = new();
        private readonly EmailService _servicioEmail = new();

        private Usuario? _usuarioObjetivo;
        private string? _codigoEnviado;

        public RecoverPasswordWindow()
        {
            InitializeComponent();
        }

        // [4] Envía un código de recuperación al correo del usuario si existe y está verificado.
        private async void BtnEnviarCodigo_Click(object sender, RoutedEventArgs e)
        {
            LblError.Text = string.Empty;

            var email = TxtEmail.Text?.Trim();
            if (string.IsNullOrEmpty(email))
            {
                LblError.Text = "Ingresa tu correo electrónico";
                return;
            }

            var usuario = _servicioAlmacenamiento.ObtenerUsuarioPorEmail(email);
            if (usuario == null)
            {
                LblError.Text = "No existe un usuario asociado a ese correo";
                return;
            }

            if (!usuario.EmailVerificado)
            {
                LblError.Text = "El correo no está verificado. Primero verifícalo desde Ajustes.";
                return;
            }

            _usuarioObjetivo = usuario;
            _codigoEnviado = UtilidadesValidacion.GenerarCodigoVerificacion();

            await _servicioEmail.EnviarCodigoAsync(email, "Recuperación de contraseña - InverTrack",
                $"Tu código de recuperación de contraseña es: {_codigoEnviado}");

            CodigoPanel.Visibility = Visibility.Visible;
            LblError.Text = "Se envió un código a tu correo. Ingrésalo junto con la nueva contraseña.";
        }

        // [4] Valida el código y aplica la nueva contraseña si todo es correcto.
        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioObjetivo == null || string.IsNullOrEmpty(_codigoEnviado))
            {
                LblError.Text = "Primero solicita un código de recuperación.";
                return;
            }

            var codigoIngresado = TxtCodigo.Text?.Trim();
            if (string.IsNullOrEmpty(codigoIngresado))
            {
                LblError.Text = "Ingresa el código de verificación";
                return;
            }

            if (!string.Equals(codigoIngresado, _codigoEnviado))
            {
                LblError.Text = "Código incorrecto";
                return;
            }

            var nueva = TxtNuevaContrasena.Password;
            var confirmar = TxtConfirmarContrasena.Password;

            if (string.IsNullOrEmpty(nueva) || string.IsNullOrEmpty(confirmar))
            {
                LblError.Text = "Ingresa y confirma la nueva contraseña";
                return;
            }

            if (nueva != confirmar)
            {
                LblError.Text = "Las contraseñas no coinciden";
                return;
            }

            _usuarioObjetivo.Contrasena = nueva;
            _servicioAlmacenamiento.GuardarUsuario(_usuarioObjetivo);

            MessageBox.Show("Contraseña actualizada correctamente.", "Recuperación", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

    }
}
