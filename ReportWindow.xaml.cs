using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using InverTrack.Models;
using InverTrack.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace InverTrack
{
    // [3] Ventana de reportes de operaciones cerradas y gráfico de desempeño.
    public partial class ReportWindow : Window
    {
        private readonly StorageService _servicioAlmacenamiento = new();
        private readonly string _usuario;

        // [3] Modelo simple de una operación cerrada (compra + venta) para reportes.
        public class OperacionCerrada
        {
            public DateTime FechaCompra { get; set; }
            public DateTime FechaVenta { get; set; }
            public string Simbolo { get; set; } = string.Empty;
            public int Cantidad { get; set; }
            public decimal PrecioCompra { get; set; }
            public decimal PrecioVenta { get; set; }
            public decimal TotalCompra { get; set; }
            public decimal TotalVenta { get; set; }
            public decimal Ganancia { get; set; }
        }

        public ReportWindow(string usuario)
        {
            InitializeComponent();
            _usuario = usuario;
            CargarReporte();
        }

        // [3] Carga las transacciones del usuario y arma la lista de operaciones cerradas.
        private void CargarReporte()
        {
            var transacciones = _servicioAlmacenamiento.ObtenerTransaccionesUsuario(_usuario)
                .OrderBy(t => t.Fecha)
                .ToList();

            if (transacciones.Count == 0)
                return;

            var compras = transacciones.Where(t => t.Tipo == "Compra").ToList();
            var ventas = transacciones.Where(t => t.Tipo == "Venta").ToList();

            // Construir operaciones cerradas emparejando cada venta con la última compra previa
            var operacionesCerradas = new List<OperacionCerrada>();

            foreach (var venta in ventas)
            {
                var comprasPrevias = compras
                    .Where(c => c.Simbolo == venta.Simbolo && c.Fecha <= venta.Fecha)
                    .OrderBy(c => c.Fecha)
                    .ToList();

                var ultimaCompra = comprasPrevias.LastOrDefault();
                decimal precioCompraUnitario = ultimaCompra?.Precio ?? venta.Precio;
                decimal totalCompra = Math.Round(precioCompraUnitario * venta.Cantidad, 2);

                var op = new OperacionCerrada
                {
                    // Usar la última compra previa como fecha de compra
                    FechaCompra = ultimaCompra?.Fecha ?? venta.Fecha,
                    FechaVenta = venta.Fecha,
                    Simbolo = venta.Simbolo,
                    Cantidad = venta.Cantidad,
                    PrecioCompra = Math.Round(precioCompraUnitario, 2),
                    PrecioVenta = venta.Precio,
                    TotalCompra = totalCompra,
                    TotalVenta = venta.Total,
                    Ganancia = venta.Total - totalCompra
                };

                operacionesCerradas.Add(op);
            }

            // Datos generales: solo mostrar la cantidad total de transacciones realizadas
            ResumenGeneral.Text = $"Transacciones realizadas: {transacciones.Count}";

            OperacionesGrid.ItemsSource = operacionesCerradas
                .OrderByDescending(o => o.FechaVenta)
                .ToList();

            ConstruirGraficoMejoresAcciones(operacionesCerradas);
        }

    }
}
