using System.Windows;
using InverTrack.Models;
using InverTrack.Services;
using InverTrack.Utils;

namespace InverTrack
{
    // [4] Ventana de registro independiente (flujo alterno al login principal).
    public partial class RegisterWindow : Window
    {
        private readonly StorageService _servicioAlmacenamiento = new();
        private readonly EmailService _servicioEmail = new();

        private string? _codigoRegistroPendiente;
        private Usuario? _usuarioRegistroPendiente;

        public RegisterWindow()
        {
            InitializeComponent();
        }

        // [4] Cierra la ventana sin crear el usuario.
        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        // [4] Valida datos básicos y registra un usuario simple desde esta ventana.
        private async void BtnCrear_Click(object sender, RoutedEventArgs e)
        {
            var usuario = TxtUsuario.Text?.Trim();
            var email = TxtEmail.Text?.Trim();
            var contrasena = TxtContrasena.Password;
            var dineroText = TxtDinero.Text?.Trim();

            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(contrasena) ||
                string.IsNullOrEmpty(dineroText) || string.IsNullOrEmpty(email))
            {
                LblError.Text = "Por favor completa todos los campos";
                return;
            }

            if (_servicioAlmacenamiento.ObtenerUsuario(usuario) != null)
            {
                LblError.Text = "El usuario ya existe";
                return;
            }

            if (_servicioAlmacenamiento.ObtenerUsuarioPorEmail(email) != null)
            {
                LblError.Text = "Ya existe un usuario con ese correo";
                return;
            }

            if (!decimal.TryParse(dineroText, out var dinero) || dinero <= 0)
            {
                LblError.Text = "Ingresa una cantidad válida de dinero";
                return;
            }

            if (!UtilidadesValidacion.EsEmailValido(email))
            {
                LblError.Text = "Ingresa un correo electrónico válido";
                return;
            }

            var nuevoUsuario = new Usuario
            {
                NombreUsuario = usuario,
                Contrasena = contrasena,
                Dinero = dinero,
                Email = email,
                EmailVerificado = false
            };

            var codigo = UtilidadesValidacion.GenerarCodigoVerificacion();
            _codigoRegistroPendiente = codigo;
            _usuarioRegistroPendiente = nuevoUsuario;

            await _servicioEmail.EnviarCodigoAsync(email, "Verificación de correo - InverTrack",
                $"Tu código de verificación para InverTrack es: {codigo}");

            MessageBox.Show("Se envió un código de verificación a tu correo. Una vez validado podrás iniciar sesión.",
                "Verificación de correo", MessageBoxButton.OK, MessageBoxImage.Information);

            // Guardamos el usuario como ya creado pero marcando EmailVerificado=false;
            // la app de login usará solo usuarios con correo verificado para recuperación.
            _servicioAlmacenamiento.GuardarUsuario(nuevoUsuario);

            DialogResult = true;
            Close();
        }
    }
}
