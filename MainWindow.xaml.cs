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


        // Codigo de diego 


        // [3] Actualiza los labels de precio actual y engancha los eventos para recalcular totales.
        private void ActualizarPrecios()
        {
            // Usar intradía si hay datos; si no, caer al histórico
            List<PrecioHistorico> fuente = _preciosIntradia.Any() ? _preciosIntradia : _preciosHistoricos;
            if (fuente.Count == 0) return;

            var precioActual = fuente.Last().Precio;

            if (!string.IsNullOrEmpty(_accionSeleccionada))
            {
                _ultimoPrecioPorSimbolo[_accionSeleccionada] = precioActual;
            }

            PrecioCompra.Text = $"${precioActual:F2}";
            PrecioVenta.Text = $"${precioActual:F2}";

            CantidadCompra.TextChanged -= Cantidad_TextChanged;
            CantidadVenta.TextChanged -= Cantidad_TextChanged;
            CantidadCompra.TextChanged += Cantidad_TextChanged;
            CantidadVenta.TextChanged += Cantidad_TextChanged;
        }

        // [3] Cada vez que cambia una cantidad, recalculamos los totales de compra/venta.
        private void Cantidad_TextChanged(object sender, TextChangedEventArgs e)
        {
            ActualizarTotal();
        }

        // [3] Calcula el total de la operación de compra y venta usando el precio canónico.
        private void ActualizarTotal()
        {
            // Precio canónico: el mismo que se usa en la gráfica, en Mi Cartera y en las operaciones.
            decimal precio;

            if (!string.IsNullOrEmpty(_accionSeleccionada) &&
                _ultimoPrecioPorSimbolo.TryGetValue(_accionSeleccionada, out var precioDict) &&
                precioDict > 0)
            {
                precio = precioDict;
            }
            else
            {
                List<PrecioHistorico> fuente = _preciosIntradia.Any() ? _preciosIntradia : _preciosHistoricos;
                if (fuente.Count == 0) return;
                precio = fuente.Last().Precio;
            }

            if (decimal.TryParse(CantidadCompra.Text, out decimal cantCompra))
                TotalCompra.Text = $"Total: ${(cantCompra * precio):F2}";

            if (decimal.TryParse(CantidadVenta.Text, out decimal cantVenta))
                TotalVenta.Text = $"Total: ${(cantVenta * precio):F2}";
        }

        // ===== [3] Sección: operaciones de compra/venta y cartera =====

        // [3] Ejecuta una compra al precio actual y actualiza usuario, cartera y gráfica.
        private void BtnComprar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_accionSeleccionada))
            {
                MessageBox.Show("Selecciona una acción");
                return;
            }

            if (!decimal.TryParse(CantidadCompra.Text, out decimal cantidad) || cantidad <= 0)
            {
                MessageBox.Show("Cantidad inválida");
                return;
            }

            if (_usuarioActual == null)
            {
                MessageBox.Show("Usuario no cargado");
                return;
            }

            // Precio canónico: usar el último precio registrado para el símbolo (el mismo que Mi Cartera y la gráfica)
            decimal precio;
            if (!string.IsNullOrEmpty(_accionSeleccionada) &&
                _ultimoPrecioPorSimbolo.TryGetValue(_accionSeleccionada, out var precioDict) &&
                precioDict > 0)
            {
                precio = precioDict;
            }
            else if (!decimal.TryParse(PrecioCompra.Text.Replace("$", string.Empty), NumberStyles.Any, CultureInfo.InvariantCulture, out precio))
            {
                // Fallback: usar la última muestra de la serie si por algún motivo no se puede parsear el texto
                List<PrecioHistorico> fuenteFallback = _preciosIntradia.Any() ? _preciosIntradia : _preciosHistoricos;
                if (fuenteFallback.Count == 0)
                {
                    MessageBox.Show("No se pudo obtener el precio actual.");
                    return;
                }
                precio = fuenteFallback.Last().Precio;
            }
            var total = cantidad * precio;
            var ahora = DateTime.Now;

            // Añadir punto de precio exacto en el modo intradía para que la marca coincida con la línea
            _preciosIntradia.Add(new PrecioHistorico
            {
                Fecha = ahora,
                Precio = precio,
                PrecioApertura = precio,
                PrecioMaximo = precio,
                PrecioMinimo = precio,
                Simbolo = _accionSeleccionada
            });

            if (!string.IsNullOrEmpty(_accionSeleccionada))
            {
                _cacheIntradia[_accionSeleccionada] = _preciosIntradia.ToList();
            }

            if (_usuarioActual.Dinero < total)
            {
                MessageBox.Show("Dinero insuficiente");
                return;
            }

            _usuarioActual.Dinero -= total;
            if (!_usuarioActual.Acciones.ContainsKey(_accionSeleccionada))
                _usuarioActual.Acciones[_accionSeleccionada] = 0;
            _usuarioActual.Acciones[_accionSeleccionada] += (int)cantidad;

            var transaccion = new Transaccion
            {
                Usuario = _usuarioActual.NombreUsuario,
                Simbolo = _accionSeleccionada,
                Tipo = "Compra",
                Cantidad = (int)cantidad,
                Precio = precio,
                Total = total,
                Fecha = ahora
            };

            _servicioAlmacenamiento.GuardarUsuario(_usuarioActual);
            _servicioAlmacenamiento.GuardarTransaccion(transaccion);

            ActualizarInfoUsuario();
            // No recargar histórico desde Yahoo al comprar; solo actualizar con los datos que ya tenemos
            ActualizarGrafica();
            ActualizarPrecios();
            CantidadCompra.Clear();

            MessageBox.Show($"Compra realizada: {cantidad} x {_accionSeleccionada} @ ${precio:F2}");
        }

        // [3] Ejecuta una venta al precio actual y actualiza usuario, cartera y gráfica.
        private void BtnVender_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_accionSeleccionada))
            {
                MessageBox.Show("Selecciona una acción");
                return;
            }

            if (!decimal.TryParse(CantidadVenta.Text, out decimal cantidad) || cantidad <= 0)
            {
                MessageBox.Show("Cantidad inválida");
                return;
            }

            if (_usuarioActual == null)
            {
                MessageBox.Show("Usuario no cargado");
                return;
            }

            if (!_usuarioActual.Acciones.ContainsKey(_accionSeleccionada) || _usuarioActual.Acciones[_accionSeleccionada] < cantidad)
            {
                MessageBox.Show("No tienes suficientes acciones");
                return;
            }

            // Precio canónico: usar el último precio registrado para el símbolo (el mismo que Mi Cartera y la gráfica)
            decimal precio;
            if (!string.IsNullOrEmpty(_accionSeleccionada) &&
                _ultimoPrecioPorSimbolo.TryGetValue(_accionSeleccionada, out var precioDict) &&
                precioDict > 0)
            {
                precio = precioDict;
            }
            else if (!decimal.TryParse(PrecioVenta.Text.Replace("$", string.Empty), NumberStyles.Any, CultureInfo.InvariantCulture, out precio))
            {
                // Fallback: usar la última muestra de la serie si por algún motivo no se puede parsear el texto
                List<PrecioHistorico> fuenteFallback = _preciosIntradia.Any() ? _preciosIntradia : _preciosHistoricos;
                if (fuenteFallback.Count == 0)
                {
                    MessageBox.Show("No se pudo obtener el precio actual.");
                    return;
                }
                precio = fuenteFallback.Last().Precio;
            }
            var total = cantidad * precio;
            var ahora = DateTime.Now;

            // Añadir punto de precio exacto en el modo intradía para que la marca coincida con la línea
            _preciosIntradia.Add(new PrecioHistorico
            {
                Fecha = ahora,
                Precio = precio,
                PrecioApertura = precio,
                PrecioMaximo = precio,
                PrecioMinimo = precio,
                Simbolo = _accionSeleccionada
            });

            if (!string.IsNullOrEmpty(_accionSeleccionada))
            {
                _cacheIntradia[_accionSeleccionada] = _preciosIntradia.ToList();
            }

            _usuarioActual.Dinero += total;
            _usuarioActual.Acciones[_accionSeleccionada] -= (int)cantidad;

            if (_usuarioActual.Acciones[_accionSeleccionada] == 0)
                _usuarioActual.Acciones.Remove(_accionSeleccionada);

            var transaccion = new Transaccion
            {
                Usuario = _usuarioActual.NombreUsuario,
                Simbolo = _accionSeleccionada,
                Tipo = "Venta",
                Cantidad = (int)cantidad,
                Precio = precio,
                Total = total,
                Fecha = ahora
            };

            _servicioAlmacenamiento.GuardarUsuario(_usuarioActual);
            _servicioAlmacenamiento.GuardarTransaccion(transaccion);

            ActualizarInfoUsuario();
            // No recargar histórico desde Yahoo al vender; solo actualizar con los datos que ya tenemos
            ActualizarGrafica();
            ActualizarPrecios();
            CantidadVenta.Clear();

            MessageBox.Show($"Venta realizada: {cantidad} x {_accionSeleccionada} @ ${precio:F2}");
        }

        // [3] Recalcula dinero disponible, valor de cartera y la lista "Mi Cartera".
        private void ActualizarInfoUsuario()
        {
            if (_usuarioActual == null)
                return;

            LblDinero.Text = $"${_usuarioActual.Dinero:F2}";

            Task.Run(async () =>
            {
                var transacciones = _servicioAlmacenamiento.ObtenerTransaccionesUsuario(_usuarioActual.NombreUsuario);
                var items = new List<CarteraItem>();
                decimal valorCartera = 0;

                foreach (var kvp in _usuarioActual.Acciones)
                {
                    var simbolo = kvp.Key;
                    var cantidad = kvp.Value;

                    // Usar SIEMPRE el precio canónico almacenado en _ultimoPrecioPorSimbolo.
                    // Si no existe para ese símbolo aún, obtenerlo UNA vez del servicio y guardarlo.
                    decimal precioActual;
                    if (!_ultimoPrecioPorSimbolo.TryGetValue(simbolo, out precioActual) || precioActual <= 0)
                    {
                        precioActual = await _servicioMercado.ObtenerPrecioActual(simbolo);
                        _ultimoPrecioPorSimbolo[simbolo] = precioActual;
                    }

                    var valorActual = precioActual * cantidad;
                    valorCartera += valorActual;

                    var transaccionesSimbolo = transacciones
                        .Where(t => t.Simbolo == simbolo)
                        .OrderBy(t => t.Fecha)
                        .ToList();

                    // Calcular costo promedio de la posición ABIERTA usando FIFO (solo compras no vendidas)
                    var lotes = new List<(int Cantidad, decimal Precio)>();

                    foreach (var t in transaccionesSimbolo)
                    {
                        if (t.Tipo == "Compra")
                        {
                            lotes.Add((t.Cantidad, t.Precio));
                        }
                        else if (t.Tipo == "Venta")
                        {
                            int porVender = t.Cantidad;
                            while (porVender > 0 && lotes.Count > 0)
                            {
                                var lote = lotes[0];
                                if (porVender >= lote.Cantidad)
                                {
                                    porVender -= lote.Cantidad;
                                    lotes.RemoveAt(0);
                                }
                                else
                                {
                                    lote.Cantidad -= porVender;
                                    lotes[0] = lote;
                                    porVender = 0;
                                }
                            }
                        }
                    }

                    int cantidadAbierta = lotes.Sum(l => l.Cantidad);
                    decimal costoAbierto = lotes.Sum(l => l.Cantidad * l.Precio);
                    decimal promedioCompra = cantidadAbierta > 0 ? costoAbierto / cantidadAbierta : 0m;

                    // Fallback por si algo no cuadra con la cantidad almacenada en el usuario
                    if (cantidadAbierta == 0 && cantidad > 0)
                    {
                        var compras = transaccionesSimbolo.Where(t => t.Tipo == "Compra").ToList();
                        var accionesCompradas = compras.Sum(t => t.Cantidad);
                        var costoTotalCompras = compras.Sum(t => t.Total);
                        promedioCompra = accionesCompradas > 0 ? costoTotalCompras / accionesCompradas : 0m;
                    }

                    var valorBase = promedioCompra * cantidad;
                    var ganancia = valorActual - valorBase;

                    items.Add(new CarteraItem
                    {
                        Simbolo = simbolo,
                        Cantidad = cantidad,
                        ValorActual = valorActual,
                        Ganancia = ganancia
                    });
                }

                Dispatcher.Invoke(() =>
                {
                    LblCartera.Text = $"${valorCartera:F2}";
                    CarteraList.ItemsSource = items;
                });
            });
        }

        // [3] Obtiene el precio promedio de la posición ABIERTA (FIFO) para un símbolo.
        private decimal CalcularPromedioCompraAbierto(string simbolo)
        {
            if (_usuarioActual == null)
                return 0m;

            var transaccionesSimbolo = _servicioAlmacenamiento.ObtenerTransaccionesUsuario(_usuarioActual.NombreUsuario)
                .Where(t => t.Simbolo == simbolo)
                .OrderBy(t => t.Fecha)
                .ToList();

            var lotes = new List<(int Cantidad, decimal Precio)>();

            foreach (var t in transaccionesSimbolo)
            {
                if (t.Tipo == "Compra")
                {
                    lotes.Add((t.Cantidad, t.Precio));
                }
                else if (t.Tipo == "Venta")
                {
                    int porVender = t.Cantidad;
                    while (porVender > 0 && lotes.Count > 0)
                    {
                        var lote = lotes[0];
                        if (porVender >= lote.Cantidad)
                        {
                            porVender -= lote.Cantidad;
                            lotes.RemoveAt(0);
                        }
                        else
                        {
                            lote.Cantidad -= porVender;
                            lotes[0] = lote;
                            porVender = 0;
                        }
                    }
                }
            }

            int cantidadAbierta = lotes.Sum(l => l.Cantidad);
            if (cantidadAbierta <= 0)
                return 0m;

            decimal costoAbierto = lotes.Sum(l => l.Cantidad * l.Precio);
            return costoAbierto > 0 ? costoAbierto / cantidadAbierta : 0m;
        }

        //Continuacion de codigo de diego
        //Inicio del codigo de caleb 

        // [3] Incrementa en +1 la cantidad de compra.
        private void BtnMasCompra_Click(object sender, RoutedEventArgs e)
        {
            IncrementarCantidad(CantidadCompra, 1);
        }

        // [3] Decrementa en -1 la cantidad de compra (sin bajar de cero).
        private void BtnMenosCompra_Click(object sender, RoutedEventArgs e)
        {
            IncrementarCantidad(CantidadCompra, -1);
        }

        // [3] Incrementa en +1 la cantidad de venta.
        private void BtnMasVenta_Click(object sender, RoutedEventArgs e)
        {
            IncrementarCantidad(CantidadVenta, 1);
        }

        // [3] Decrementa en -1 la cantidad de venta (sin bajar de cero).
        private void BtnMenosVenta_Click(object sender, RoutedEventArgs e)
        {
            IncrementarCantidad(CantidadVenta, -1);
        }
    }
}
