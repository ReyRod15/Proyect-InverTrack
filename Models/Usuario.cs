using System;
using System.Collections.Generic;

namespace InverTrack.Models
{
    // [1] Modelo principal de usuario usado en autenticaciÃ³n, cartera y reportes.
    public class Usuario
    {
        public string NombreUsuario { get; set; } = string.Empty;
        public string Contrasena { get; set; } = string.Empty;
        public decimal Dinero { get; set; }
}
}
