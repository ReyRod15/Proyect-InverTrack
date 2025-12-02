using System;
using System.Collections.Generic;

namespace InverTrack.Models
{
    // [1] Modelo principal de usuario usado en autenticación, cartera y reportes.
    public class Usuario
    {
        public string NombreUsuario { get; set; } = string.Empty;
        public string Contrasena { get; set; } = string.Empty;
        public decimal Dinero { get; set; }
        public Dictionary<string, int> Acciones { get; set; } = new();
        public DateTime FechaCreacion { get; set; }

        // Nuevo: correo electrónico y flag de verificación
        public string Email { get; set; } = string.Empty;
        public bool EmailVerificado { get; set; }

        // Guardamos la fecha en que se crea el usuario por primera vez.
        public Usuario()
        {
            FechaCreacion = DateTime.Now;
        }
    }
}
