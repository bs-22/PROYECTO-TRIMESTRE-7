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

            // Filtramos las reuniones donde el ID del usuario aparece en asistencia_reunion
            var reuniones = (from r in db.Reunion
                             join a in db.AsistenciaReunion
                             on r.ID_reunion equals a.ID_reunion
                             where a.ID_usuario == idUsuarioActual
                             select r).Distinct().AsQueryable();

            if (!string.IsNullOrWhiteSpace(valor))
            {
                if (criterio == "ID" && decimal.TryParse(valor, out decimal id))
                    reuniones = reuniones.Where(r => r.ID_reunion == id);
                else if (criterio == "Fecha" && DateTime.TryParse(valor, out DateTime fecha))
                    reuniones = reuniones.Where(r => r.fecha_reunion == fecha);
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