using System;

namespace InverTrack.Models
{
    // [2] Modelo de punto de precio historico (usado en graficos y simulación)
    public class PrecioHistorico
    {
        public DateTime Fecha { get; set; }

        // Precio de cierre (se usa en la graficas y para calculos existentes)
        public decimal Precio { get; set; }

        // Para velas (OHLC). Si no se rellenan explicitamente, se pueden asumir = Precio.
        public decimal PrecioApertura { get; set; }
        public decimal PrecioMaximo { get; set; }
        public decimal PrecioMinimo { get; set; }

        public string Simbolo { get; set; } = string.Empty;
    }
}
