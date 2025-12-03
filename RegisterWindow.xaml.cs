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

    }
}
