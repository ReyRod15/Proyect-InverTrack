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



        // [1] Ventana principal del simulador; inicializa la interfaz y el usuario activo.
        public MainWindow(string? nombreUsuario = null)
        {
            InitializeComponent();
            InitializeApplication(nombreUsuario);
        }

        // [1] Resuelve el usuario a usar, actualiza etiquetas y carga acciones iniciales.
        private async void InitializeApplication(string? nombreUsuario = null)
        {
            // Obtener usuario desde parámetro o usar por defecto
            _usuarioActual = !string.IsNullOrEmpty(nombreUsuario)
                ? _servicioAlmacenamiento.ObtenerUsuario(nombreUsuario)
                : null;

            _usuarioActual ??= _servicioAlmacenamiento.ObtenerUsuario("admin")
                ?? new Usuario { NombreUsuario = "admin", Dinero = 10000 };

            LblUsuario.Text = _usuarioActual.NombreUsuario;
            ActualizarInfoUsuario();

            await CargarAcciones();

            IniciarTimerActualizacion();
        }

        // [1] Carga la lista de símbolos disponibles y su precio actual simulado.
        private async Task CargarAcciones()
        {
            var acciones = _servicioMercado.ObtenerAccionesDisponibles();
            var listaAcciones = new List<dynamic>();

            foreach (var simbolo in acciones)
            {
                try
                {
                    var precio = await _servicioMercado.ObtenerPrecioActual(simbolo);
                    _ultimoPrecioPorSimbolo[simbolo] = precio;
                    listaAcciones.Add(new { Simbolo = simbolo, Precio = precio });
                }
                catch
                {
                    _ultimoPrecioPorSimbolo[simbolo] = 0m;
                    listaAcciones.Add(new { Simbolo = simbolo, Precio = 0m });
                }
            }

            AccionesList.ItemsSource = listaAcciones;
        }

        // [1] Al cambiar de acción en la lista, guarda la anterior en cache y carga la nueva gráfica.
        private async void AccionesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AccionesList.SelectedItem == null) return;

            dynamic item = AccionesList.SelectedItem;

            // Guardar en cache los datos de la acción anterior antes de cambiar
            if (!string.IsNullOrEmpty(_accionSeleccionada))
            {
                _cacheHistorico[_accionSeleccionada] = _preciosHistoricos.ToList();
                _cacheIntradia[_accionSeleccionada] = _preciosIntradia.ToList();
            }

            _accionSeleccionada = item.Simbolo;

            await CargarGrafica();
        }

        // [1] Carga histórico e intradía del símbolo seleccionado y ajusta la gráfica.
        private async Task CargarGrafica()
        {
            if (string.IsNullOrEmpty(_accionSeleccionada))
                return;

            DateTime hasta = DateTime.Now;
            // Cargar siempre varios años hacia atrás para poder mostrar vistas de meses/años
            DateTime desde = hasta.AddYears(-3);

            // Intentar usar cache local primero
            if (_cacheHistorico.TryGetValue(_accionSeleccionada, out var histCache) && histCache.Any())
            {
                _preciosHistoricos = histCache.ToList();
            }
            else
            {
                try
                {
                    _preciosHistoricos = await _servicioMercado.ObtenerHistorico(_accionSeleccionada, desde, hasta);
                    _cacheHistorico[_accionSeleccionada] = _preciosHistoricos.ToList();
                }
                catch
                {
                    _preciosHistoricos = new List<PrecioHistorico>();
                }
            }

            if (_cacheIntradia.TryGetValue(_accionSeleccionada, out var intraCache) && intraCache.Any())
            {
                _preciosIntradia = intraCache.ToList();
            }
            else
            {
                _preciosIntradia = new List<PrecioHistorico>();

                // Crear primer punto intradía con el precio actual si no hay cache aún
                try
                {
                    var precioActual = await _servicioMercado.ObtenerPrecioActual(_accionSeleccionada);
                    if (precioActual > 0)
                    {
                        _preciosIntradia.Add(new PrecioHistorico
                        {
                            Fecha = DateTime.Now,
                            Precio = precioActual,
                            PrecioApertura = precioActual,
                            PrecioMaximo = precioActual,
                            PrecioMinimo = precioActual,
                            Simbolo = _accionSeleccionada
                        });
                    }
                }
                catch
                {
                    // si falla, nos quedamos solo con histórico
                }

                _cacheIntradia[_accionSeleccionada] = _preciosIntradia.ToList();
            }

            // Alinear el último punto histórico con el precio más reciente que usamos en el simulador
            // para que el valor al final de la gráfica (ej. 28 de noviembre) coincida con el precio actual
            // mostrado en la vista "Actual" y no haya diferencias de muchos dólares.
            decimal precioActualReferencia = 0m;
            if (_preciosIntradia.Any())
            {
                precioActualReferencia = _preciosIntradia.Last().Precio;
            }
            else if (!string.IsNullOrEmpty(_accionSeleccionada) &&
                     _ultimoPrecioPorSimbolo.TryGetValue(_accionSeleccionada, out var precioDict) &&
                     precioDict > 0)
            {
                precioActualReferencia = precioDict;
            }

            if (precioActualReferencia > 0 && _preciosHistoricos.Any())
            {
                var ultimoHist = _preciosHistoricos.OrderBy(p => p.Fecha).Last();
                ultimoHist.Precio = precioActualReferencia;

                if (ultimoHist.PrecioMaximo == 0 || ultimoHist.PrecioMaximo < precioActualReferencia)
                    ultimoHist.PrecioMaximo = precioActualReferencia;
                if (ultimoHist.PrecioMinimo == 0 || ultimoHist.PrecioMinimo > precioActualReferencia)
                    ultimoHist.PrecioMinimo = precioActualReferencia;
                if (ultimoHist.PrecioApertura == 0)
                    ultimoHist.PrecioApertura = precioActualReferencia;

                if (!string.IsNullOrEmpty(_accionSeleccionada))
                {
                    _cacheHistorico[_accionSeleccionada] = _preciosHistoricos.ToList();
                }
            }

            _debeRecalcularRangoX = true;

            // Mostrar si los datos que alimentan el histórico son simulados o reales
            ActualizarIndicadorDatosSimulados(_servicioMercado.UltimoHistoricoEsSimulado);

            // Al cambiar de acción, recalculamos el eje Y desde cero para que
            // el rango vertical se adapte al nuevo valor de la acción y no se
            // hereden rangos exagerados de la acción anterior.
            AsegurarModeloGrafica();
            if (_ejeY != null)
            {
                _ejeY.Minimum = double.NaN;
                _ejeY.Maximum = double.NaN;
            }

            ActualizarGrafica();
            ActualizarPrecios();
        }
    }
        // ===== [2] Sección: gráfica principal y control de zoom =====

        // [2] Crea el modelo de la gráfica (ejes, series y anotaciones) si aún no existe.
        private void AsegurarModeloGrafica()
        {
            if (_modeloGrafica != null && _ejeX != null && _ejeY != null && _seriePrecio != null && _serieLineaCompra != null && _serieVelas != null)
                return;

            _modeloGrafica = new PlotModel
            {
                Background = OxyColors.Transparent,
                PlotAreaBorderColor = OxyColor.FromRgb(209, 213, 219),
                TextColor = OxyColor.FromRgb(17, 24, 39)
            };

            _ejeX = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                IsZoomEnabled = true,
                IsPanEnabled = true,
                // Rango mínimo de 1 minuto para evitar zoom excesivo
                MinimumRange = TimeSpan.FromMinutes(1).TotalDays
            };
            // Nota: ya no usamos AxisChanged para lógica adicional de zoom, se deja sin manejador.
            _modeloGrafica.Axes.Add(_ejeX);

            _ejeY = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Precio ($)",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                IsZoomEnabled = false,
                IsPanEnabled = false
            };
            _modeloGrafica.Axes.Add(_ejeY);

            _seriePrecio = new LineSeries
            {
                Title = "Precio",
                Color = OxyColor.FromRgb(59, 130, 246),
                StrokeThickness = 1.8
            }; // azul suave

            _serieLineaCompra = new LineSeries
            {
                Title = "Nivel compra",
                Color = OxyColor.FromRgb(34, 197, 94),
                LineStyle = LineStyle.Dash,
                StrokeThickness = 1.2
            };

            _serieVelas = new CandleStickSeries
            {
                Title = "Velas",
                Color = OxyColor.FromRgb(55, 65, 81), // borde
                IncreasingColor = OxyColor.FromRgb(34, 197, 94),
                DecreasingColor = OxyColor.FromRgb(239, 68, 68),
                StrokeThickness = 1,
                CandleWidth = 0.5
            };

            _modeloGrafica.Series.Add(_seriePrecio);
            _modeloGrafica.Series.Add(_serieLineaCompra);
            _modeloGrafica.Series.Add(_serieVelas);

            // Anotación para el precio actual sobre el punto de la gráfica
            _anotacionPrecioActual = new OxyPlot.Annotations.TextAnnotation
            {
                Text = string.Empty,
                Stroke = OxyColors.Transparent,
                Background = OxyColor.FromAColor(180, OxyColors.White),
                TextColor = OxyColor.FromRgb(17, 24, 39),
                Padding = new OxyThickness(4),
                FontSize = 10,
                Layer = OxyPlot.Annotations.AnnotationLayer.AboveSeries
            };
            _modeloGrafica.Annotations.Add(_anotacionPrecioActual);

            AplicarTemaGrafica();

            PrecioPlot.Model = _modeloGrafica;
        }

        // [2] Redibuja la gráfica según el modo de vista (actual/meses/años) y la serie disponible.
        private void ActualizarGrafica()
        {
            if (string.IsNullOrEmpty(_accionSeleccionada)) return;

            AsegurarModeloGrafica();

            var simbolo = _accionSeleccionada ?? string.Empty;
            var nombreAccion = ObtenerNombreAccionLegible(simbolo);
            _modeloGrafica!.Title = string.IsNullOrEmpty(simbolo)
                ? nombreAccion
                : $"{nombreAccion} ({simbolo})";

            // Alternar visibilidad entre línea y velas según el modo de visualización seleccionado
            if (_usarVelas)
            {
                // Modo velas: ocultamos la línea y mostramos las velas en cualquier vista (Actual / Meses / Años)
                if (_seriePrecio != null) _seriePrecio.IsVisible = false;
                if (_serieVelas != null) _serieVelas.IsVisible = true;
            }
            else
            {
                // Modo línea: solo la línea está visible
                if (_seriePrecio != null) _seriePrecio.IsVisible = true;
                if (_serieVelas != null) _serieVelas.IsVisible = false;
            }

            // Formato de eje X según modo de vista seleccionado
            if (_ejeX != null)
            {
                if (_modoVista == "actual")
                {
                    // Vista ACTUAL: ventana corta en minutos, eje por minuto (HH:mm)
                    _ejeX.StringFormat = "HH:mm";
                    _ejeX.IntervalType = DateTimeIntervalType.Minutes;
                    _ejeX.MajorStep = TimeSpan.FromMinutes(1).TotalDays;
                    _ejeX.LabelFormatter = null; // etiquetas por defecto (HH:mm)
                }
                else if (_modoVista == "meses")
                {
                    // Vista ÚLTIMOS MESES: eje mensual (etiqueta una sola vez por mes)
                    _ejeX.StringFormat = null;
                    _ejeX.IntervalType = DateTimeIntervalType.Months;
                    _ejeX.MajorStep = 1; // 1 mes entre marcas
                    _ejeX.LabelFormatter = null; // se configurará más abajo
                }
                else // "anios": varios años, eje mensual
                {
                    _ejeX.StringFormat = null;
                    _ejeX.IntervalType = DateTimeIntervalType.Months;
                    _ejeX.MajorStep = 1; // 1 mes entre marcas
                    _ejeX.LabelFormatter = null; // se configurará más abajo
                }

                // Estilo de líneas de la cuadrícula para evitar demasiadas líneas en vista de años
                if (_modoVista == "anios")
                {
                    _ejeX.MajorGridlineStyle = LineStyle.None;
                    _ejeX.MinorGridlineStyle = LineStyle.None;
                }
                else
                {
                    _ejeX.MajorGridlineStyle = LineStyle.Solid;
                    _ejeX.MinorGridlineStyle = LineStyle.Dot;
                }
            }

            // Elegir modo: intradía (vista ACTUAL) o histórico (últimos meses / años)
            bool esIntradia = _modoVista == "actual";

            List<PrecioHistorico> fuente;
            if (esIntradia && _preciosIntradia.Any())
            {
                fuente = _preciosIntradia;
            }
            else if (_preciosHistoricos.Any())
            {
                fuente = _preciosHistoricos;
            }
            else if (_preciosIntradia.Any())
            {
                // Fallback raro: sin histórico pero con algo de intradía
                fuente = _preciosIntradia;
            }
            else
            {
                return;
            }

            // Siempre mantenemos todos los puntos dibujados y solo movemos la "ventana" visible con el eje X
            var todos = fuente
                .OrderBy(p => p.Fecha)
                .ToList();

            _seriePrecio!.Points.Clear();
            foreach (var p in todos)
            {
                _seriePrecio.Points.Add(new DataPoint(DateTimeAxis.ToDouble(p.Fecha), (double)p.Precio));
            }

            // Rellenar serie de velas usando OHLC
            if (_serieVelas != null)
            {
                _serieVelas.Items.Clear();

                if (_usarVelas)
                {
                    if (_modoVista == "actual")
                    {
                        // En modo ACTUAL usamos velas de 30 segundos de datos,
                        // pero dibujadas con un poco menos de anchura para que no se "choquen" visualmente.
                        double width = TimeSpan.FromSeconds(20).TotalDays; // grosor visual ~20s, datos de 30s
                        _serieVelas.CandleWidth = width;

                        var grupos = todos.GroupBy(p =>
                            new DateTime(
                                p.Fecha.Year,
                                p.Fecha.Month,
                                p.Fecha.Day,
                                p.Fecha.Hour,
                                p.Fecha.Minute,
                                p.Fecha.Second < 30 ? 0 : 30));

                        foreach (var g in grupos)
                        {
                            var ordenados = g.OrderBy(p => p.Fecha).ToList();
                            if (ordenados.Count == 0) continue;

                            decimal open = ordenados.First().PrecioApertura != 0 ? ordenados.First().PrecioApertura : ordenados.First().Precio;
                            decimal close = ordenados.Last().Precio;
                            decimal high = g.Max(p => p.PrecioMaximo != 0 ? p.PrecioMaximo : p.Precio);
                            decimal low = g.Min(p => p.PrecioMinimo != 0 ? p.PrecioMinimo : p.Precio);

                            double x = DateTimeAxis.ToDouble(ordenados.First().Fecha);
                            _serieVelas.Items.Add(new HighLowItem(x, (double)high, (double)low, (double)open, (double)close));
                        }
                    }
                    else
                    {
                        if (_modoVista == "meses")
                        {
                            // En vista de MESES usamos velas DIARIAS (cada punto histórico es un día)
                            _serieVelas.CandleWidth = TimeSpan.FromDays(0.7).TotalDays;

                            foreach (var p in todos)
                            {
                                decimal open = p.PrecioApertura != 0 ? p.PrecioApertura : p.Precio;
                                decimal close = p.Precio;
                                decimal high = p.PrecioMaximo != 0 ? p.PrecioMaximo : p.Precio;
                                decimal low = p.PrecioMinimo != 0 ? p.PrecioMinimo : p.Precio;

                                double x = DateTimeAxis.ToDouble(p.Fecha);
                                _serieVelas.Items.Add(new HighLowItem(x, (double)high, (double)low, (double)open, (double)close));
                            }
                        }
                        else // "anios": agrupar por SEMANAS para que se entienda mejor la gráfica a largo plazo
                        {
                            var culture = System.Globalization.CultureInfo.CurrentCulture;
                            var calendar = culture.Calendar;
                            var weekRule = culture.DateTimeFormat.CalendarWeekRule;
                            var firstDay = culture.DateTimeFormat.FirstDayOfWeek;

                            _serieVelas.CandleWidth = TimeSpan.FromDays(5).TotalDays; // ~una semana de trading

                            var gruposSemana = todos.GroupBy(p =>
                            {
                                int week = calendar.GetWeekOfYear(p.Fecha, weekRule, firstDay);
                                return new { p.Fecha.Year, Week = week };
                            });

                            foreach (var g in gruposSemana.OrderBy(g => g.Min(p => p.Fecha)))
                            {
                                var ordenados = g.OrderBy(p => p.Fecha).ToList();
                                if (ordenados.Count == 0) continue;

                                var fechaGrupo = ordenados.First().Fecha;

                                decimal open = ordenados.First().PrecioApertura != 0 ? ordenados.First().PrecioApertura : ordenados.First().Precio;
                                decimal close = ordenados.Last().Precio;
                                decimal high = g.Max(p => p.PrecioMaximo != 0 ? p.PrecioMaximo : p.Precio);
                                decimal low = g.Min(p => p.PrecioMinimo != 0 ? p.PrecioMinimo : p.Precio);

                                double x = DateTimeAxis.ToDouble(fechaGrupo);
                                _serieVelas.Items.Add(new HighLowItem(x, (double)high, (double)low, (double)open, (double)close));
                            }
                        }
                    }
                }
            }

            // Etiquetas del eje X según el modo (meses / años)
            if (_ejeX != null)
            {
                if (_modoVista == "meses")
                {
                    // Mostrar solo una etiqueta por mes, al inicio de cada mes (Ene, Feb, ...)
                    _ejeX.LabelFormatter = v =>
                    {
                        var d = _ejeX!.ConvertToDateTime(v);
                        return d.Day == 1 ? d.ToString("MMM") : string.Empty;
                    };
                }
                else if (_modoVista == "anios")
                {
                    // Vista de años: al inicio de cada año mostrar solo el año; resto de meses solo el nombre del mes
                    _ejeX.LabelFormatter = v =>
                    {
                        var d = _ejeX!.ConvertToDateTime(v);
                        if (d.Day != 1) return string.Empty;
                        return d.Month == 1 ? d.ToString("yyyy") : d.ToString("MMM");
                    };
                }
                else if (_modoVista == "actual")
                {
                    _ejeX.LabelFormatter = null; // volver a formato HH:mm por defecto
                }
            }

            // Actualizar anotación de precio actual en la gráfica
            if (_anotacionPrecioActual != null && todos.Count > 0)
            {
                var ultimo = todos.Last();
                var x = DateTimeAxis.ToDouble(ultimo.Fecha);
                var y = (double)ultimo.Precio;
                _anotacionPrecioActual.Text = $"${ultimo.Precio:F2}";
                _anotacionPrecioActual.TextPosition = new DataPoint(x, y);

                // Calcular precio promedio de compra si hay posición abierta
                // Color del texto del precio actual: ajustamos según tema (claro/oscuro)
                OxyColor colorTexto = _modoOscuro
                    ? OxyColor.FromRgb(248, 250, 252) // casi blanco en modo oscuro
                    : OxyColor.FromRgb(17, 24, 39);   // casi negro en modo claro

                if (_usuarioActual != null &&
                    _usuarioActual.Acciones.TryGetValue(_accionSeleccionada ?? string.Empty, out int cantidadActual) &&
                    cantidadActual > 0)
                {
                    // Usar el mismo promedio FIFO de posición abierta que en "Mi Cartera"
                    decimal promedioCompra = CalcularPromedioCompraAbierto(_accionSeleccionada!);

                    if (promedioCompra > 0)
                    {
                        if (ultimo.Precio > promedioCompra)
                        {
                            // Verde más brillante en modo oscuro para que destaque
                            colorTexto = _modoOscuro
                                ? OxyColor.FromRgb(52, 211, 153)
                                : OxyColor.FromRgb(16, 185, 129);
                        }
                        else if (ultimo.Precio < promedioCompra)
                        {
                            // Rojo más brillante en modo oscuro para que destaque
                            colorTexto = _modoOscuro
                                ? OxyColor.FromRgb(248, 113, 113)
                                : OxyColor.FromRgb(239, 68, 68);
                        }
                    }
                }

                _anotacionPrecioActual.TextColor = colorTexto;
            }

            // Transacciones históricas del usuario para esta acción (para precio promedio de compra)
            var transacciones = _servicioAlmacenamiento.ObtenerTransaccionesUsuario(_usuarioActual!.NombreUsuario)
                .Where(t => t.Simbolo == _accionSeleccionada)
                .OrderBy(t => t.Fecha)
                .ToList();

            // Línea horizontal de precio de compra si el usuario tiene posición abierta
            // Solo se muestra en la vista ACTUAL; en meses/años no se dibuja para evitar que quede "muy abajo".
            if (_serieLineaCompra != null)
            {
                _serieLineaCompra.Points.Clear();

                if (_modoVista == "actual" &&
                    _usuarioActual != null &&
                    !string.IsNullOrEmpty(_accionSeleccionada) &&
                    _usuarioActual.Acciones.TryGetValue(_accionSeleccionada, out int cantidadActual) && cantidadActual > 0)
                {
                    // Usar el mismo promedio de compra FIFO de la posición abierta que en "Mi Cartera"
                    decimal promedioCompra = CalcularPromedioCompraAbierto(_accionSeleccionada!);

                    if (promedioCompra > 0)
                    {
                        var minFecha = todos.First().Fecha;
                        var maxFecha = todos.Last().Fecha;
                        var y = (double)promedioCompra;

                        _serieLineaCompra.Points.Add(new DataPoint(DateTimeAxis.ToDouble(minFecha), y));
                        _serieLineaCompra.Points.Add(new DataPoint(DateTimeAxis.ToDouble(maxFecha), y));
                    }
                }
            }

            // Auto-scroll / zoom del eje X según modo de vista
            if (_ejeX != null && todos.Count > 0)
            {
                if (_modoVista == "actual")
                {
                    // Vista ACTUAL: siempre centramos un rango de ~1 minuto alrededor del último punto
                    var ultimo = todos.Last();
                    var desdeVentana = ultimo.Fecha.AddMinutes(-1);
                    var hastaVentana = ultimo.Fecha.AddSeconds(10); // pequeño margen hacia adelante

                    _ejeX.Minimum = DateTimeAxis.ToDouble(desdeVentana);
                    _ejeX.Maximum = DateTimeAxis.ToDouble(hastaVentana);
                }
                else if (_debeRecalcularRangoX && _modoVista == "meses")
                {
                    // Últimos 6 meses hasta hoy
                    DateTime hasta = DateTime.Now;
                    DateTime desde = hasta.AddMonths(-6);

                    var ventana = todos.Where(p => p.Fecha >= desde).ToList();
                    if (ventana.Count == 0)
                        ventana = todos;

                    var minFecha = ventana.First().Fecha;
                    var maxFecha = ventana.Last().Fecha;
                    _ejeX.Minimum = DateTimeAxis.ToDouble(minFecha);
                    _ejeX.Maximum = DateTimeAxis.ToDouble(maxFecha.AddDays(0.2));
                }
                else if (_debeRecalcularRangoX && _modoVista == "anios")
                {
                    // Vista de años: mostrar todo el histórico cargado
                    var minFecha = todos.First().Fecha;
                    var maxFecha = todos.Last().Fecha;

                    // Ajustar a inicios de mes para que las marcas salgan al principio
                    var minMes = new DateTime(minFecha.Year, minFecha.Month, 1);
                    var maxMes = new DateTime(maxFecha.Year, maxFecha.Month, 1).AddMonths(1);

                    _ejeX.Minimum = DateTimeAxis.ToDouble(minMes);
                    _ejeX.Maximum = DateTimeAxis.ToDouble(maxMes);
                }

                // Para modos históricos, una vez fijado el rango X no lo volvemos a mover automáticamente
                if (_modoVista != "actual")
                {
                    _debeRecalcularRangoX = false;
                }
            }

            // Ajustar eje Y de forma estable: evitamos que la serie "salga" de la pantalla
            // y reducimos cambios bruscos en el rango vertical.
            if (_ejeY != null && todos.Count > 0)
            {
                var visibles = todos.Where(p => DateTimeAxis.ToDouble(p.Fecha) >= (_ejeX?.Minimum ?? double.MinValue)
                                             && DateTimeAxis.ToDouble(p.Fecha) <= (_ejeX?.Maximum ?? double.MaxValue)).ToList();
                if (!visibles.Any()) visibles = todos;

                // En modo de años usamos SIEMPRE todos los puntos para el rango Y, para ver completamente toda la serie
                var baseListaY = _modoVista == "anios" ? todos : visibles;

                var minPrecio = (double)baseListaY.Min(p => p.Precio);
                var maxPrecio = (double)baseListaY.Max(p => p.Precio);

                // En vista ACTUAL, incluir también el nivel de compra del usuario (si existe)
                // para que la línea horizontal sea visible dentro del rango Y.
                if (_modoVista == "actual" &&
                    _usuarioActual != null &&
                    !string.IsNullOrEmpty(_accionSeleccionada) &&
                    _usuarioActual.Acciones.TryGetValue(_accionSeleccionada, out int cantActual) && cantActual > 0)
                {
                    // Incluir el nivel de compra promedio de la posición ABIERTA (FIFO)
                    decimal promedioCompraY = CalcularPromedioCompraAbierto(_accionSeleccionada!);

                    if (promedioCompraY > 0)
                    {
                        minPrecio = Math.Min(minPrecio, (double)promedioCompraY);
                        maxPrecio = Math.Max(maxPrecio, (double)promedioCompraY);
                    }
                }

                // Evitar rangos exagerados en comparación con el precio actual SOLO en vistas de meses/años.
                if (_modoVista == "meses" || _modoVista == "anios")
                {
                    var ultimoPrecio = (double)todos.Last().Precio;
                    if (ultimoPrecio > 0)
                    {
                        double minPermitido = ultimoPrecio * 0.5;  // no mostrar por debajo del 50% del precio actual
                        double maxPermitido = ultimoPrecio * 1.5;  // ni por encima del 150% del precio actual
                        minPrecio = Math.Max(minPrecio, minPermitido);
                        maxPrecio = Math.Min(maxPrecio, maxPermitido);
                    }
                }

                if (minPrecio == maxPrecio)
                {
                    // Evitar rango cero
                    minPrecio *= 0.95;
                    maxPrecio *= 1.05;
                }

                var margen = (maxPrecio - minPrecio) * 0.1;
                if (margen <= 0) margen = Math.Max(1, maxPrecio * 0.05);

                double targetMin = minPrecio - margen;
                double targetMax = maxPrecio + margen;

                if (double.IsNaN(_ejeY.Minimum) || double.IsNaN(_ejeY.Maximum))
                {
                    // Primera vez: fijar directamente
                    _ejeY.Minimum = targetMin;
                    _ejeY.Maximum = targetMax;
                }
                else
                {
                    // Solo ampliar si es necesario para incluir nuevos valores; no encoger
                    if (targetMin < _ejeY.Minimum)
                        _ejeY.Minimum = targetMin;
                    if (targetMax > _ejeY.Maximum)
                        _ejeY.Maximum = targetMax;
                }
            }

            _modeloGrafica.InvalidatePlot(true);
        }
        // [2] Activa el modo de velas japonesas en la gráfica.
        private void ToggleVelas_Checked(object sender, RoutedEventArgs e)
        {
            _usarVelas = true;
            ActualizarGrafica();
        }

        // [2] Vuelve al modo de línea simple.
        private void ToggleVelas_Unchecked(object sender, RoutedEventArgs e)
        {
            _usarVelas = false;
            ActualizarGrafica();
        }
        // [2] Ajusta los colores de la gráfica (OxyPlot) al tema actual.
        private void AplicarTemaGrafica()
        {
            if (_modeloGrafica == null || _ejeX == null || _ejeY == null)
                return;

            if (_modoOscuro)
            {
                _modeloGrafica.TextColor = OxyColor.FromRgb(249, 250, 251);
                _modeloGrafica.PlotAreaBorderColor = OxyColor.FromRgb(55, 65, 81);
                _ejeX.MajorGridlineColor = OxyColor.FromRgb(55, 65, 81);
                _ejeX.MinorGridlineColor = OxyColor.FromRgb(31, 41, 55);
                _ejeY.MajorGridlineColor = OxyColor.FromRgb(55, 65, 81);
                _ejeY.MinorGridlineColor = OxyColor.FromRgb(31, 41, 55);
                if (_anotacionPrecioActual != null)
                {
                    _anotacionPrecioActual.Background = OxyColor.FromArgb(220, 31, 41, 55);
                }
            }
            else
            {
                _modeloGrafica.TextColor = OxyColor.FromRgb(17, 24, 39);
                _modeloGrafica.PlotAreaBorderColor = OxyColor.FromRgb(209, 213, 219);
                _ejeX.MajorGridlineColor = OxyColor.FromRgb(209, 213, 219);
                _ejeX.MinorGridlineColor = OxyColor.FromRgb(229, 231, 235);
                _ejeY.MajorGridlineColor = OxyColor.FromRgb(209, 213, 219);
                _ejeY.MinorGridlineColor = OxyColor.FromRgb(229, 231, 235);
                if (_anotacionPrecioActual != null)
                {
                    _anotacionPrecioActual.Background = OxyColor.FromAColor(180, OxyColors.White);
                }
            }
            _modeloGrafica.InvalidatePlot(false);
        }
        // [2] Mantiene el intervalo del timer de actualización en un valor fijo (3s).
        private void AjustarIntervaloPorRango()
        {
            // Intervalo fijo de 3 segundos, independientemente del rango o zoom
            if (_temporizadorActualizacion == null)
                return;

            _intervaloSegundos = 3;
            _temporizadorActualizacion.Interval = TimeSpan.FromSeconds(_intervaloSegundos);
        }
    }
}
