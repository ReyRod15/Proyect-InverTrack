using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using InverTrack.Models;
using InverTrack.Services;

namespace InverTrack
{
    public partial class MainWindow : Window
    {
        private readonly StorageService _servicioAlmacenamiento = new();
        // Servicio de datos de mercado ficticios/simulados
        private readonly CsvMarketDataService _servicioMercado = new();
        // ReporteService se deja inyectado si en el futuro se quiere exportar reportes a archivos
        // privados, pero actualmente no se usa directamente en MainWindow.
        private Usuario? _usuarioActual;
        private string? _accionSeleccionada;
        // Histórico (diario) cargado desde Yahoo para vistas de meses/años
        private List<PrecioHistorico> _preciosHistoricos = new();
        // Intradía de la sesión (puntos cada pocos segundos desde que se selecciona la acción)
        private List<PrecioHistorico> _preciosIntradia = new();
        // Cache por símbolo para no perder la gráfica al cambiar de acción
        private readonly Dictionary<string, List<PrecioHistorico>> _cacheHistorico = new();
        private readonly Dictionary<string, List<PrecioHistorico>> _cacheIntradia = new();
        // Último precio conocido por símbolo (para sincronizar gráfica y "Mi cartera")
        private readonly Dictionary<string, decimal> _ultimoPrecioPorSimbolo = new();
        private PlotModel? _modeloGrafica;
        private DispatcherTimer? _temporizadorActualizacion;
        private DispatcherTimer? _temporizadorPrecioRapido; // actualiza solo el valor mostrado cada 1s
        private int _intervaloSegundos = 3; // actualización automática cada 3s para la gráfica
        private bool _modoOscuro = false;
        // Modo de vista actual de la gráfica: "actual", "meses", "anios"
        private string _modoVista = "actual";
        // Indica si debemos recalcular el rango del eje X (se activa al cambiar de vista/acción o al pulsar Actualizar)
        private bool _debeRecalcularRangoX = true;
        private readonly Random _aleatorioIntradia = new Random();
        private LineSeries? _seriePrecio;
        private LineSeries? _serieLineaCompra;
        private CandleStickSeries? _serieVelas;
        private bool _usarVelas = false;
        private DateTimeAxis? _ejeX;
        private LinearAxis? _ejeY;
        private OxyPlot.Annotations.TextAnnotation? _anotacionPrecioActual;
    }
}
