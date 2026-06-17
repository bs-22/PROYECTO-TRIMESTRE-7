using GestionSemillero1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Globalization;

namespace GestionSemillero1.Controllers
{
    public class InvestigadorController : Controller
    {
        private DbSemillero db = new DbSemillero();

        // GET: Investigador/Index
        // Acción principal del Investigador: Lista las reuniones asignadas al usuario logueado con opciones de búsqueda.
        public ActionResult Index(string criterio, string valor)
        {
            // Validación de seguridad: Verifica que exista una sesión activa.
            if (Session["IDUsuario"] == null) return RedirectToAction("Login", "Account");

            decimal idUsuarioActual = Convert.ToDecimal(Session["IDUsuario"]);

            // Consulta LINQ: Une las tablas de reuniones y asistencia para obtener solo los registros vinculados al investigador actual.
            var queryBase = (from r in db.Reunion
                             join a in db.AsistenciaReunion
                             on r.ID_reunion equals a.ID_reunion
                             where a.ID_usuario == idUsuarioActual
                             select r).Distinct();

            // Precarga de filtros (ViewBag): Obtiene los 5 IDs y fechas más recientes para alimentar los buscadores.
            ViewBag.TopIDs = queryBase.Select(r => r.ID_reunion)
                            .Distinct()
                            .Take(5)
                            .AsEnumerable()
                            .Select(id => id.ToString())
                            .ToList();

            var fechasRaw = queryBase.Select(r => r.fecha_reunion)
                                    .Distinct()
                                    .Take(5)
                                    .ToList();

            // Formateo de fechas: Asegura el formato "dd/MM/yyyy" para consistencia en la interfaz.
            ViewBag.TopFechas = fechasRaw
                               .Select(f => f != null ? Convert.ToDateTime(f).ToString("dd/MM/yyyy") : "")
                               .Where(x => !string.IsNullOrEmpty(x))
                               .ToList();

            // Lógica de filtrado: Aplica condiciones dinámicas según el criterio seleccionado (ID o Fecha).
            var reuniones = queryBase.AsQueryable();

            if (!string.IsNullOrWhiteSpace(valor))
            {
                if (criterio == "ID" && decimal.TryParse(valor, out decimal id))
                {
                    reuniones = reuniones.Where(r => r.ID_reunion == id);
                }
                else if (criterio == "Fecha")
                {
                    // Intenta parsear la fecha exacta respetando el formato regional para evitar errores culturales.
                    if (DateTime.TryParseExact(valor, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fechaExacta))
                    {
                        reuniones = reuniones.Where(r => r.fecha_reunion == fechaExacta);
                    }
                    else if (DateTime.TryParse(valor, out DateTime fecha))
                    {
                        reuniones = reuniones.Where(r => r.fecha_reunion == fecha);
                    }
                }
            }

            return View(reuniones.ToList());
        }
        // Método de limpieza de recursos (Override del método base).
        // Se ejecuta automáticamente cuando el controlador termina su ciclo de vida (request HTTP finalizado).
        protected override void Dispose(bool disposing)
        {
            // Verifica si la propiedad 'disposing' es verdadera, lo que significa que el objeto 
            // debe liberar explícitamente sus recursos.
            if (disposing)
            {
                // Libera la conexión a la base de datos (db). Esto es crucial para evitar 
                // "fugas de memoria" o que el pool de conexiones SQL se sature.
                db.Dispose();
            }

            // Llama al método Dispose de la clase padre (Controller) para completar la limpieza estándar.
            base.Dispose(disposing);
        }
    }
}