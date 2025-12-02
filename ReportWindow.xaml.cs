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

    }
}
