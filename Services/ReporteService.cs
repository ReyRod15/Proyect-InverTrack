using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InverTrack.Models;
using Newtonsoft.Json;

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
    }
}
