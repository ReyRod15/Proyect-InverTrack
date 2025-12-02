using InverTrack.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace InverTrack.Services
{
    // [3] Servicio de reportes (archivos JSON y estadi­sticas de operaciones).
    public class ReporteService
    {
        // [3] Dependencias y rutas
        private readonly StorageService _servicioAlmacenamiento;
        private readonly string _carpetaReportes;

        // [3] Constructor
        public ReporteService()
        {
            _servicioAlmacenamiento = new StorageService();
            _carpetaReportes = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "InverTrack", "Reportes");

            if (!Directory.Exists(_carpetaReportes))
            {
                Directory.CreateDirectory(_carpetaReportes);
            }
        }

        // [3] Genera un resumen JSON simple con transacciones y datos agregados.
        public string GenerarReportePDF(string usuario)
        {
            var transacciones = _servicioAlmacenamiento.ObtenerTransaccionesUsuario(usuario);

            if (transacciones.Count == 0)
                return "No hay transacciones para este usuario";

            var resumen = new
            {
                Usuario = usuario,
                FechaReporte = DateTime.Now,
                TotalTransacciones = transacciones.Count,
                TotalCompras = transacciones.Where(t => t.Tipo == "Compra").Count(),
                TotalVentas = transacciones.Where(t => t.Tipo == "Venta").Count(),
                GastoTotal = transacciones.Where(t => t.Tipo == "Compra").Sum(t => t.Total),
                IngresoTotal = transacciones.Where(t => t.Tipo == "Venta").Sum(t => t.Total),
                AccionesTradeadas = transacciones.Select(t => t.Simbolo).Distinct().ToList(),
                Transacciones = transacciones.OrderByDescending(t => t.Fecha).ToList()
            };

            var nombreArchivo = $"Reporte_{usuario}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var rutaArchivo = Path.Combine(_carpetaReportes, nombreArchivo);

            File.WriteAllText(rutaArchivo, JsonConvert.SerializeObject(resumen, Formatting.Indented));

            return rutaArchivo;
        }

        // [3] Calcula estadísticas básicas de compras, ventas y ganancia/perdida.
        public object ObtenerEstadisticas(string usuario)
        {
            var transacciones = _servicioAlmacenamiento.ObtenerTransaccionesUsuario(usuario);

            if (transacciones.Count == 0)
                return new { Mensaje = "No hay transacciones" };

            var compras = transacciones.Where(t => t.Tipo == "Compra").ToList();
            var ventas = transacciones.Where(t => t.Tipo == "Venta").ToList();

            var gananciaPerdida = ventas.Sum(t => t.Total) - compras.Sum(t => t.Total);
            var porcentajeGanancia = compras.Count > 0 ? (gananciaPerdida / compras.Sum(t => t.Total)) * 100 : 0;

            var estadisticasPorAccion = transacciones
                .GroupBy(t => t.Simbolo)
                .Select(g => new
                {
                    Simbolo = g.Key,
                    TotalOperaciones = g.Count(),
                    Compras = g.Where(t => t.Tipo == "Compra").Count(),
                    Ventas = g.Where(t => t.Tipo == "Venta").Count(),
                    TotalInvertido = g.Where(t => t.Tipo == "Compra").Sum(t => t.Total),
                    TotalObtenido = g.Where(t => t.Tipo == "Venta").Sum(t => t.Total)
                })
                .ToList();

            return new
            {
                Usuario = usuario,
                FechaReporte = DateTime.Now,
                TotalTransacciones = transacciones.Count,
                TotalCompras = compras.Count,
                TotalVentas = ventas.Count,
                GastoTotal = compras.Sum(t => t.Total),
                IngresoTotal = ventas.Sum(t => t.Total),
                GananciaPerdida = gananciaPerdida,
                PorcentajeGanancia = porcentajeGanancia,
                TransaccionPromedio = transacciones.Average(t => t.Total),
                PrimeraTransaccion = transacciones.Min(t => t.Fecha),
                UltimaTransaccion = transacciones.Max(t => t.Fecha),
                EstadisticasPorAccion = estadisticasPorAccion
            };
        }
    }
}
