using System.Windows;
using System.Windows.Input;
using InverTrack.Models;
using InverTrack.Services;
using InverTrack.Utils;

namespace InverTrack
{
    // [4] Ventana principal de autenticacion (login y registro).
    public partial class AuthWindow : Window
    {
        private readonly StorageService _servicioAlmacenamiento = new();
        private readonly EmailService _servicioEmail = new();

        private string? _codigoRegistroPendiente;
        private Usuario? _usuarioRegistroPendiente;

        public AuthWindow()
        {
            InitializeComponent();
            ErrorMessage.Visibility = Visibility.Collapsed;
        }


        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            var usuario = LoginUsername.Text;
            var contrasena = LoginPassword.Password;

            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(contrasena))
            {
                ErrorMessage.Text = "Por favor completa todos los campos";
                ErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            var usuarioDb = _servicioAlmacenamiento.ObtenerUsuario(usuario);
            if (usuarioDb == null || usuarioDb.Contrasena != contrasena)
            {
                ErrorMessage.Text = "Usuario o contraseña incorrectos";
                ErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            ErrorMessage.Text = string.Empty;
            ErrorMessage.Visibility = Visibility.Collapsed;

            MainWindow mainWindow = new(usuarioDb.NombreUsuario);
            mainWindow.Show();
            Close();
        }

        // [4] Cambia del panel de login al formulario de registro.
        private void BtnToRegister_Click(object sender, RoutedEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Collapsed;
            RegisterPanel.Visibility = Visibility.Visible;
            ErrorMessage.Text = string.Empty;
            ErrorMessage.Visibility = Visibility.Collapsed;
        }

        // [4] Vuelve del formulario de registro al panel de login.
        private void BtnToLogin_Click(object sender, RoutedEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Visible;
            RegisterPanel.Visibility = Visibility.Collapsed;
            ErrorMessage.Text = string.Empty;
            ErrorMessage.Visibility = Visibility.Collapsed;
        }


        // [4] Valida los datos de registro y envía un código de verificación al correo.
        private async void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            var usuario = RegisterUsername.Text?.Trim();
            var email = RegisterEmail.Text?.Trim();
            var contrasena = RegisterPassword.Password;
            var dineroText = RegisterMoney.Text?.Trim();

            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(contrasena) ||
                string.IsNullOrEmpty(dineroText) || string.IsNullOrEmpty(email))
            {
                ErrorMessage.Text = "Por favor completa todos los campos";
                ErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            if (_servicioAlmacenamiento.ObtenerUsuario(usuario) != null)
            {
                ErrorMessage.Text = "El usuario ya existe";
                ErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            if (_servicioAlmacenamiento.ObtenerUsuarioPorEmail(email) != null)
            {
                ErrorMessage.Text = "Ya existe un usuario con ese correo";
                ErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            if (!decimal.TryParse(dineroText, out decimal dinero) || dinero <= 0)
            {
                ErrorMessage.Text = "Ingresa una cantidad válida";
                ErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            if (!UtilidadesValidacion.EsEmailValido(email))
            {
                ErrorMessage.Text = "Ingresa un correo electrónico válido";
                ErrorMessage.Visibility = Visibility.Visible;
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

            // Generar código de verificación de 6 dígitos
            var codigo = UtilidadesValidacion.GenerarCodigoVerificacion();
            _codigoRegistroPendiente = codigo;
            _usuarioRegistroPendiente = nuevoUsuario;

            await _servicioEmail.EnviarCodigoAsync(email, "Verificación de correo - InverTrack",
                $"Tu código de verificación para InverTrack es: {codigo}");

            ErrorMessage.Text = "Se envió un código de verificación a tu correo. Ingresa el código para completar el registro.";
            ErrorMessage.Visibility = Visibility.Visible;
            RegisterCodePanel.Visibility = Visibility.Visible;
        }


        // [4] Permite hacer login presionando Enter en los campos de usuario/contraseña.
        private void Login_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnLogin_Click(sender, e);
            }
        }

        // [4] Confirma el código de registro y crea la cuenta si todo coincide.
        private async void BtnConfirmRegisterCode_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioRegistroPendiente == null || string.IsNullOrEmpty(_codigoRegistroPendiente))
            {
                ErrorMessage.Text = "No hay un registro pendiente. Vuelve a completar el formulario.";
                ErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            var codigoIngresado = RegisterCode.Text?.Trim();
            if (string.IsNullOrEmpty(codigoIngresado))
            {
                ErrorMessage.Text = "Ingresa el código de verificación";
                ErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            if (!string.Equals(codigoIngresado, _codigoRegistroPendiente))
            {
                ErrorMessage.Text = "Código incorrecto";
                ErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            _usuarioRegistroPendiente.EmailVerificado = true;
            _servicioAlmacenamiento.GuardarUsuario(_usuarioRegistroPendiente);

            ErrorMessage.Text = string.Empty;
            ErrorMessage.Visibility = Visibility.Collapsed;

            // Iniciar sesión automáticamente después del registro
            MainWindow mainWindow = new(_usuarioRegistroPendiente.NombreUsuario);
            mainWindow.Show();
            Close();
        }

        // [4] Abre la ventana de recuperación de contraseña.
        private void BtnForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new RecoverPasswordWindow();
            ventana.Owner = this;
            ventana.ShowDialog();
        }
    }
}
