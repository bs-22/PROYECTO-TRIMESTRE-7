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
            if (Session["IDUsuario"] == null)
                return Json(new { success = true, mensaje = "Tu sesión ha expirado, por favor inicia sesión nuevamente." });

            int idUsuario = Convert.ToInt32(Session["IDUsuario"]);
            string rol = Session["TipoUsuario"]?.ToString();
            string datosContexto = "No se encontraron datos.";

            // 2. Lógica por Rol (Basada en tu DB)
            try
            {
                switch (rol)
                {
                    case "Investigador":
                        var inv = db.investigadores.FirstOrDefault(i => i.ID_usuario == idUsuario);
                        int totalReuniones = db.Reunion.Count(r => r.AsistenciaReunion.Any(a => a.ID_usuario == idUsuario));
                        datosContexto = $"Eres Investigador. Tienes {totalReuniones} reuniones asignadas.";
                        break;

                    case "Lider":
                        var liderInv = db.investigadores.FirstOrDefault(i => i.ID_usuario == idUsuario);
                        if (liderInv != null)
                        {
                            var sem = db.semillero.FirstOrDefault(s => s.ID_semillero == liderInv.ID_semillero);
                            int cantInvestigadores = db.investigadores.Count(i => i.ID_semillero == liderInv.ID_semillero);
                            int cantProyectos = db.Proyectos.Count(p => p.ID_semillero == liderInv.ID_semillero);

                            datosContexto = $"Eres Líder del semillero: {sem?.nombre_semillero}. " +
                                            $"Tienes {cantInvestigadores} investigadores a cargo y {cantProyectos} proyectos activos.";
                        }
                        break;

                    case "Administrador":
                        int totalSemilleros = db.semillero.Count();
                        int totalInvestigadores = db.investigadores.Count();
                        datosContexto = $"Eres Administrador. El sistema tiene {totalSemilleros} semilleros y {totalInvestigadores} investigadores en total.";
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