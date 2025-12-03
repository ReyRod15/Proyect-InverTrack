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
    }
}
