using InverTrack.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace InverTrack.Services
{
    // [1] Servicio de almacenamiento en disco para usuarios y transacciones.
    public class StorageService
    {
        // [1] Rutas de trabajo en disco
        private readonly string _carpetaDatos;
        private readonly string _rutaUsuarios;
        private readonly string _rutaTransacciones;

        // [1] Constructor
        public StorageService()
        {
            _carpetaDatos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "InverTrack", "Data");
            _rutaUsuarios = Path.Combine(_carpetaDatos, "usuarios.json");
            _rutaTransacciones = Path.Combine(_carpetaDatos, "transacciones.json");

            if (!Directory.Exists(_carpetaDatos))
            {
                Directory.CreateDirectory(_carpetaDatos);
            }

            InicializarArchivos();
        }

        // [1] Métodos de carga / preparación de datos
        private void InicializarArchivos()
        {
            if (!File.Exists(_rutaUsuarios))
            {
                File.WriteAllText(_rutaUsuarios, "[]");
            }

            if (!File.Exists(_rutaTransacciones))
            {
                File.WriteAllText(_rutaTransacciones, "[]");
            }
        }

        // [1] Métodos principales de persistencia
        public void GuardarUsuario(Usuario usuario)
        {
            var usuarios = CargarUsuarios();
            var usuarioExistente = usuarios.FirstOrDefault(u => u.NombreUsuario == usuario.NombreUsuario);

            if (usuarioExistente != null)
            {
                usuarios.Remove(usuarioExistente);
            }

            usuarios.Add(usuario);
            File.WriteAllText(_rutaUsuarios, JsonConvert.SerializeObject(usuarios, Formatting.Indented));
        }

        public Usuario? ObtenerUsuario(string nombreUsuario)
        {
            var usuarios = CargarUsuarios();
            return usuarios.FirstOrDefault(u => u.NombreUsuario == nombreUsuario);
        }

        // [1] Busca un usuario por su correo (ignorando mayúsculas/minúsculas).
        public Usuario? ObtenerUsuarioPorEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            var usuarios = CargarUsuarios();
            return usuarios.FirstOrDefault(u =>
                !string.IsNullOrEmpty(u.Email) &&
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
        }

        // [1] Devuelve todos los usuarios guardados en disco.
        public List<Usuario> CargarUsuarios()
        {
            var json = File.ReadAllText(_rutaUsuarios);
            return JsonConvert.DeserializeObject<List<Usuario>>(json) ?? new List<Usuario>();
        }

        // [1] Agrega una nueva transacción al historial en disco.
        public void GuardarTransaccion(Transaccion transaccion)
        {
            var transacciones = CargarTransacciones();
            transacciones.Add(transaccion);
            File.WriteAllText(_rutaTransacciones, JsonConvert.SerializeObject(transacciones, Formatting.Indented));
        }

        // [1] Devuelve el historial completo de transacciones.
        public List<Transaccion> CargarTransacciones()
        {
            var json = File.ReadAllText(_rutaTransacciones);
            return JsonConvert.DeserializeObject<List<Transaccion>>(json) ?? new List<Transaccion>();
        }

        // [1] Filtra las transacciones que pertenecen a un usuario específico.
        public List<Transaccion> ObtenerTransaccionesUsuario(string usuario)
        {
            return CargarTransacciones().Where(t => t.Usuario == usuario).ToList();
        }
    }
}
