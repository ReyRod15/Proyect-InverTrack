using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InverTrack.Models;

namespace InverTrack.Services
{
    /// <summary>
    /// Servicio de datos de mercado totalmente ficticios (simulados).
    /// No depende de conexion a Internet ni de Yahoo Finance.
    /// Reynols, Lesther, Caleb y Diego: si algunon di­a conectan una API real,
    /// este es el punto central a cambiar.
    /// </summary>
    // [2] Servicio central para simular datos de mercado e histÃ³ricos.
    public class CsvMarketDataService
    {
        private readonly Dictionary<string, List<PrecioHistorico>> _cacheHistorico = new();

        // En este servicio todos los datos son simulados.
        private bool _ultimoHistoricoSimulado = true;
        public bool UltimoHistoricoEsSimulado => _ultimoHistoricoSimulado;

        // Precios "actuales" del día de hoy proporcionados por el usuario
        private static readonly Dictionary<string, decimal> PreciosHoy = new()
        {
            { "META", 628.81m },
            { "GOOGL", 173.97m },
            { "NVDA", 177.00m },
            { "AMZN", 191.09m },
            { "AMD", 186.47m },
            { "AAPL", 278.85m },
            { "TSLA", 419.47m },
        };

        // [2] Devuelve la lista fija de símbolos disponibles en el simulador.
        public string[] ObtenerAccionesDisponibles() =>
            new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA", "AMD" };

        // [2] Obtiene (o genera) el histórico simulado para un símbolo en el rango indicado.
        public Task<List<PrecioHistorico>> ObtenerHistorico(string simbolo, DateTime desde, DateTime hasta)
        {
            // Intentamos primero en memoria para no recalcular en esta misma ejecución.
            if (_cacheHistorico.TryGetValue(simbolo, out var listaEnCache) && listaEnCache.Any())
            {
                return Task.FromResult(FiltrarPorRango(listaEnCache, desde, hasta));
            }

            // Si no hay nada en memoria, generamos siempre una serie nueva de datos simulados
            // anclados a los precios actuales proporcionados.
            var listaSimulada = GenerarDatosSimulados(simbolo, desde, hasta);
            _cacheHistorico[simbolo] = listaSimulada;

            return Task.FromResult(FiltrarPorRango(listaSimulada, desde, hasta));
        }

        // [2] Usa el último cierre de los últimos años como "precio actual" del símbolo.
        public async Task<decimal> ObtenerPrecioActual(string simbolo)
        {
            // Usar el último cierre disponible de un rango razonable (por ejemplo, últimos 3 años)
            var hasta = DateTime.Today;
            var desde = hasta.AddYears(-3);
            var historicoCompleto = await ObtenerHistorico(simbolo, desde, hasta);
            return historicoCompleto.LastOrDefault()?.Precio ?? 0m;
        }

        /// <summary>
        /// [2] Genera un histórico diario simulado tipo "Yahoo" (random walk suave), y luego lo escala
        /// para que el último cierre coincida exactamente con el precio actual proporcionado.
        /// </summary>
        private List<PrecioHistorico> GenerarDatosSimulados(string simbolo, DateTime desde, DateTime hasta)
        {
            var precios = new List<PrecioHistorico>();

            // Construir rango de fechas según los parámetros (solo días hábiles)
            var fechas = new List<DateTime>();
            for (var fecha = desde.Date; fecha <= hasta.Date; fecha = fecha.AddDays(1))
            {
                if (fecha.DayOfWeek == DayOfWeek.Saturday || fecha.DayOfWeek == DayOfWeek.Sunday)
                    continue;
                fechas.Add(fecha);
            }

            if (fechas.Count == 0)
                return precios;

            // Precio base inicial: si tenemos precio actual, usar algo cercano; si no, usar tabla base.
            var precioBase = new Dictionary<string, decimal>
            {
                { "AAPL", 150m },
                { "GOOGL", 140m },
                { "MSFT", 380m },
                { "AMZN", 180m },
                { "TSLA", 240m },
                { "META", 480m },
                { "NVDA", 875m },
                { "AMD", 190m }
            };

            decimal precioInicial;
            if (!PreciosHoy.TryGetValue(simbolo, out precioInicial))
            {
                if (!precioBase.TryGetValue(simbolo, out precioInicial))
                    precioInicial = 100m;
            }

            // Empezar un poco por debajo del precio inicial para que haya espacio de subida y bajada
            decimal precioActual = Math.Max(10m, precioInicial * 0.8m);
            var random = new Random();

            foreach (var fecha in fechas)
            {
                // Variación diaria suave (~±2 unidades) tipo Yahoo original
                decimal variacion = (decimal)(random.NextDouble() - 0.5) * 4m; // -2..+2

                decimal precioApertura = precioActual;
                precioActual += variacion;
                if (precioActual < 10m) precioActual = 10m;

                decimal cierre = Math.Round(precioActual, 2);
                decimal deltaMax = (decimal)random.NextDouble() * 2m;
                decimal deltaMin = (decimal)random.NextDouble() * 2m;
                decimal max = Math.Max(precioApertura, cierre) + deltaMax;
                decimal min = Math.Min(precioApertura, cierre) - deltaMin;
                if (min < 10m) min = 10m;

                precios.Add(new PrecioHistorico
                {
                    Fecha = fecha,
                    Precio = cierre,
                    PrecioApertura = Math.Round(precioApertura, 2),
                    PrecioMaximo = Math.Round(max, 2),
                    PrecioMinimo = Math.Round(min, 2),
                    Simbolo = simbolo
                });
            }

            // Escalar toda la serie para que el último punto coincida exactamente con el precio objetivo de hoy
            if (PreciosHoy.TryGetValue(simbolo, out var objetivoHoy) && precios.Count > 0)
            {
                var ultimo = precios.OrderBy(p => p.Fecha).Last();
                if (ultimo.Precio > 0)
                {
                    decimal factor = objetivoHoy / ultimo.Precio;

                    foreach (var p in precios)
                    {
                        p.Precio = Math.Round(p.Precio * factor, 2);
                        p.PrecioApertura = Math.Round(p.PrecioApertura * factor, 2);
                        p.PrecioMaximo = Math.Round(p.PrecioMaximo * factor, 2);
                        p.PrecioMinimo = Math.Round(Math.Max(10m, p.PrecioMinimo * factor), 2);
                    }
                }
            }

            return precios.OrderBy(p => p.Fecha).ToList();
        }
    }
}  
    


