using System;

namespace InverTrack.Models
{
    // [3] Modelo de transacciÃ³n (compra/venta) usado para historiales y reportes.
    public class Transaccion
    {
        public string Usuario { get; set; } = string.Empty;
        public string Simbolo { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty; // "Compra" o "Venta"
        public int Cantidad { get; set; }
        public decimal Precio { get; set; }
        public decimal Total { get; set; }
        public DateTime Fecha { get; set; }

        // Por defecto marcamos la fecha de creaciÃ³n en el momento actual.
}
}
