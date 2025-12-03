using System;
using System.Windows;
using InverTrack.Models;
using InverTrack.Services;
using InverTrack.Utils;

namespace InverTrack
{
    // [4] Ventana de ajustes de cuenta (correo y contraseña) del usuario.
    public partial class SettingsWindow : Window
    {
        private readonly StorageService _servicioAlmacenamiento = new();
        private readonly EmailService _servicioEmail = new();
        private readonly Usuario _usuario;

        private string? _codigoEmailPendiente;
        private string? _nuevoEmailPendiente;
        private string? _codigoPwdPendiente;

        public SettingsWindow(Usuario usuario)
        {
            InitializeComponent();
            _usuario = usuario;
            CargarDatosIniciales();
        }

        // [4] Carga el estado inicial de la ventana con datos del usuario actual.
        private void CargarDatosIniciales()
        {
            LblUsuario.Text = $"Usuario: {_usuario.NombreUsuario}";

            if (string.IsNullOrEmpty(_usuario.Email))
            {
                LblCorreoActual.Text = "Correo actual: (sin correo asignado)";
            }
            else
            {
                var estado = _usuario.EmailVerificado ? "verificado" : "no verificado";
                LblCorreoActual.Text = $"Correo actual: {_usuario.Email} ({estado})";
            }

            if (string.IsNullOrEmpty(_usuario.Email))
            {
                LblInfoPassword.Text = "Para cambiar la contraseña primero debes agregar y verificar un correo electrónico.";
                BtnEnviarCodigoPwd.IsEnabled = false;
            }
            else if (!_usuario.EmailVerificado)
            {
                LblInfoPassword.Text = "Tu correo aún no está verificado. Verifícalo arriba para poder cambiar la contraseña.";
                BtnEnviarCodigoPwd.IsEnabled = false;
            }
            else
            {
                LblInfoPassword.Text = "Se enviará un código de verificación a tu correo actual para autorizar el cambio de contraseña.";
                BtnEnviarCodigoPwd.IsEnabled = true;
            }
        }

        // [4] Pide un nuevo correo y envía un código para confirmarlo.
        private async void BtnEnviarCodigoEmail_Click(object sender, RoutedEventArgs e)
        {
            LblError.Text = string.Empty;

            var nuevoEmail = TxtNuevoEmail.Text?.Trim();
            if (string.IsNullOrEmpty(nuevoEmail))
            {
                LblError.Text = "Ingresa un correo nuevo";
                return;
            }

            if (!UtilidadesValidacion.EsEmailValido(nuevoEmail))
            {
                LblError.Text = "Ingresa un correo electrónico válido";
                return;
            }

            if (!string.IsNullOrEmpty(_usuario.Email) &&
                string.Equals(_usuario.Email, nuevoEmail, StringComparison.OrdinalIgnoreCase) &&
                _usuario.EmailVerificado)
            {
                LblError.Text = "Ese correo ya está asignado y verificado.";
                return;
            }

            var existente = _servicioAlmacenamiento.ObtenerUsuarioPorEmail(nuevoEmail);
            if (existente != null && !string.Equals(existente.NombreUsuario, _usuario.NombreUsuario, StringComparison.OrdinalIgnoreCase))
            {
                LblError.Text = "Ya existe otro usuario con ese correo.";
                return;
            }

            _nuevoEmailPendiente = nuevoEmail;
            _codigoEmailPendiente = UtilidadesValidacion.GenerarCodigoVerificacion();

            await _servicioEmail.EnviarCodigoAsync(nuevoEmail, "Verificación de correo - InverTrack",
                $"Tu código de verificación de correo es: {_codigoEmailPendiente}");

            PanelCodigoEmail.Visibility = Visibility.Visible;
            LblError.Text = "Se envió un código al nuevo correo. Ingrésalo para confirmar el cambio.";
        }

        // [4] Confirma el código enviado al nuevo correo y actualiza la cuenta.
        private void BtnConfirmarEmail_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_nuevoEmailPendiente) || string.IsNullOrEmpty(_codigoEmailPendiente))
            {
                LblError.Text = "Primero envía un código al nuevo correo.";
                return;
            }

            var codigo = TxtCodigoEmail.Text?.Trim();
            if (string.IsNullOrEmpty(codigo))
            {
                LblError.Text = "Ingresa el código de verificación";
                return;
            }

            if (!string.Equals(codigo, _codigoEmailPendiente))
            {
                LblError.Text = "Código de verificación incorrecto";
                return;
            }

            _usuario.Email = _nuevoEmailPendiente;
            _usuario.EmailVerificado = true;
            _servicioAlmacenamiento.GuardarUsuario(_usuario);

            _codigoEmailPendiente = null;
            _nuevoEmailPendiente = null;
            TxtCodigoEmail.Text = string.Empty;

            MessageBox.Show("Correo actualizado y verificado correctamente.", "Ajustes", MessageBoxButton.OK, MessageBoxImage.Information);
            CargarDatosIniciales();
        }

        // [4] Envía un código al correo actual para autorizar cambio de contraseña.
        private async void BtnEnviarCodigoPwd_Click(object sender, RoutedEventArgs e)
        {
            LblError.Text = string.Empty;

            if (string.IsNullOrEmpty(_usuario.Email) || !_usuario.EmailVerificado)
            {
                LblError.Text = "Necesitas tener un correo verificado para cambiar la contraseña.";
                return;
            }

            _codigoPwdPendiente = UtilidadesValidacion.GenerarCodigoVerificacion();

            await _servicioEmail.EnviarCodigoAsync(_usuario.Email, "Cambio de contraseña - InverTrack",
                $"Tu código para cambiar la contraseña es: {_codigoPwdPendiente}");

            PanelCodigoPwd.Visibility = Visibility.Visible;
            LblError.Text = "Se envió un código a tu correo. Ingrésalo junto con la nueva contraseña.";
        }

        // [4] Valida el código y aplica la nueva contraseña.
        private void BtnConfirmarPwd_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_codigoPwdPendiente))
            {
                LblError.Text = "Primero solicita un código para cambiar la contraseña.";
                return;
            }

            var codigo = TxtCodigoPwd.Text?.Trim();
            if (string.IsNullOrEmpty(codigo))
            {
                LblError.Text = "Ingresa el código de verificación";
                return;
            }

            if (!string.Equals(codigo, _codigoPwdPendiente))
            {
                LblError.Text = "Código de verificación incorrecto";
                return;
            }

            var nueva = TxtNuevaPwd.Password;
            var confirmar = TxtConfirmarPwd.Password;

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

            _usuario.Contrasena = nueva;
            _servicioAlmacenamiento.GuardarUsuario(_usuario);

            _codigoPwdPendiente = null;
            TxtCodigoPwd.Text = string.Empty;
            TxtNuevaPwd.Password = string.Empty;
            TxtConfirmarPwd.Password = string.Empty;

            MessageBox.Show("Contraseña actualizada correctamente.", "Ajustes", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // [4] Cierra la ventana de ajustes sin más cambios.
        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }


    }
}

