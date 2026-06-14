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
        public ActionResult Index(string criterio, string valor)
        {
            if (Session["IDUsuario"] == null) return RedirectToAction("Login", "Account");

            decimal idUsuarioActual = Convert.ToDecimal(Session["IDUsuario"]);

            // 1. Consulta base: Filtramos las reuniones asignadas al investigador actual
            var queryBase = (from r in db.Reunion
                             join a in db.AsistenciaReunion
                             on r.ID_reunion equals a.ID_reunion
                             where a.ID_usuario == idUsuarioActual
                             select r).Distinct();

            // 🌟 NUEVO: Extraer los primeros 5 IDs asignados únicos para el buscador
            ViewBag.TopIDs = queryBase.Select(r => r.ID_reunion.ToString())
                                      .Distinct()
                                      .Take(5)
                                      .ToList();

            // 🌟 NUEVO: Extraer las primeras 5 Fechas asignadas únicas
            var fechasRaw = queryBase.Select(r => r.fecha_reunion)
                                     .Distinct()
                                     .Take(5)
                                     .ToList();

            // Formateamos las fechas de forma segura a "dd/MM/yyyy" para pasarlas a la vista
            ViewBag.TopFechas = fechasRaw
                                .Select(f => f != null ? Convert.ToDateTime(f).ToString("dd/MM/yyyy") : "")
                                .Where(x => !string.IsNullOrEmpty(x))
                                .ToList();

            // 2. Aplicamos la lógica de filtrado sobre el listado final si el usuario buscó algo
            var reuniones = queryBase.AsQueryable();

            if (!string.IsNullOrWhiteSpace(valor))
            {
                if (criterio == "ID" && decimal.TryParse(valor, out decimal id))
                {
                    reuniones = reuniones.Where(r => r.ID_reunion == id);
                }
                else if (criterio == "Fecha")
                {
                    // Intentamos primero el parseo exacto en formato dd/MM/yyyy para evitar choques culturales
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

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}