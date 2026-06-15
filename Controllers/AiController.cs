using GestionSemillero1.Models;
using GestionSemillero1.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace GestionSemillero1.Controllers
{
    public class AiController : Controller
    {
        private readonly AiService _aiService = new AiService();
        // 1. Instancia de tu base de datos
        private readonly DbSemillero db = new DbSemillero();
        [HttpPost]
        public async Task<ActionResult> ConsultarAsistente(string pregunta)
        {
            // 1. Validación de Sesión
            if (Session["UserId"] == null)
                return Json(new { success = true, mensaje = "Tu sesión ha expirado, por favor inicia sesión nuevamente." });

            int idUsuario = Convert.ToInt32(Session["UserId"]);
            string rol = Session["Rol"]?.ToString();
            string datosContexto = "No se encontraron datos.";

            // 2. Lógica por Rol (Basada en tu DB)
            try
            {
                switch (rol)
                {
                    case "Investigador":
                        var inv = db.investigadores.FirstOrDefault(i => i.ID_usuario == idUsuario);
                        if (inv != null)
                        {
                            // Filtro: Reuniones propias o donde fue convocado
                            var misReuniones = db.Reunion
                                .Where(r => r.ID_semillero == inv.ID_semillero
                                         || r.AsistenciaReunion.Any(a => a.ID_usuario == idUsuario))
                                .ToList();

                            datosContexto = misReuniones.Any()
                                ? "Mis reuniones: " + string.Join("; ", misReuniones.Select(r => $"{r.descripcion_reunion} ({r.fecha_reunion})"))
                                : "No tienes reuniones asignadas.";
                        }
                        break;

                    case "Lider":
                        // El Líder ve todo lo de su semillero asignado
                        var liderInv = db.investigadores.FirstOrDefault(i => i.ID_usuario == idUsuario);
                        if (liderInv != null)
                        {
                            var semilleroInfo = db.semillero.FirstOrDefault(s => s.ID_semillero == liderInv.ID_semillero);
                            var proy = db.Proyectos.Where(p => p.ID_semillero == liderInv.ID_semillero).ToList();

                            datosContexto = $"Gestionas el semillero: {semilleroInfo?.nombre_semillero}. " +
                                            $"Proyectos a cargo: {string.Join(", ", proy.Select(p => p.nombre_proyecto))}.";
                        }
                        break;

                    case "Administrador":
                        // El administrador tiene visión global
                        int totalSemilleros = db.semillero.Count();
                        datosContexto = $"Eres Administrador. El sistema cuenta con {totalSemilleros} semilleros activos. Tienes acceso total a la configuración y reportes.";
                        break;

                    default:
                        datosContexto = "Rol no reconocido. Contacta a soporte.";
                        break;
                }
            }
            catch (Exception ex)
            {
                datosContexto = "Error al consultar los datos del sistema.";
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }

            // 3. Llamada a la IA con el contexto y el rol
            string respuesta = await _aiService.GenerarRespuesta(pregunta, datosContexto, rol);

            return Json(new { success = true, mensaje = respuesta });
        }
    }
}