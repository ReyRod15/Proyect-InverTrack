using System;

namespace InverTrack.Models
{
    // [2] Modelo de punto de precio histÃ³rico (usado en grÃ¡ficos y simulaciÃ³n).
    public class PrecioHistorico
    {
        public DateTime Fecha { get; set; }

        // Precio de cierre (se usa en la grÃ¡fica de lÃ­nea y para cÃ¡lculos existentes)
        public decimal Precio { get; set; }

        // Para velas (OHLC). Si no se rellenan explÃ­citamente, se pueden asumir = Precio.
        public decimal PrecioApertura { get; set; }
        public decimal PrecioMaximo { get; set; }
        public decimal PrecioMinimo { get; set; }

        public string Simbolo { get; set; } = string.Empty;
    }
}
