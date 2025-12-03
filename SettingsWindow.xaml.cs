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


    }
}

