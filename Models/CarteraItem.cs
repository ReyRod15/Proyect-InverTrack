using System.Windows.Media;

namespace InverTrack.Models
{
    // [3] Modelo de ítem para mostrar posiciones en "Mi Cartera".
    public class CarteraItem
    {
        public string Simbolo { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal ValorActual { get; set; }
        public decimal Ganancia { get; set; }

        public string CantidadTexto => $"Cantidad: {Cantidad}";

        public string ValorYGanancia
        {
            get
            {
                var valor = ValorActual.ToString("F2");
                var diff = Ganancia.ToString("F2");
                if (Ganancia > 0)
                    return $"${valor} (+{diff})";
                if (Ganancia < 0)
                    return $"${valor} ({diff})";
                return $"${valor} (0.00)";
            }
        }

        public Brush GananciaColor => Ganancia >= 0 ? Brushes.Green : Brushes.Red;
    }
}
