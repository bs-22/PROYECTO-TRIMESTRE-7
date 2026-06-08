using GestionSemillero1.Models;
using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace GestionSemillero1.Controllers
{
    public class InvestigadorController : Controller
    {
        private DbSemillero db = new DbSemillero();

        
        public ActionResult Index(string criterioFiltro, string valorBusqueda)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            if (Session["UsuarioLogueado"] == null || Session["TipoUsuario"]?.ToString().ToLower() != "investigador")
                return RedirectToAction("Login", "Account");

            var hoy = DateTime.Today;
            var reunionesQuery = db.Reunion.AsQueryable();

            // Filtrar próximas reuniones
            reunionesQuery = reunionesQuery.Where(r => r.fecha_reunion >= hoy);

            // Filtros dinámicos basados en tu BD real
            if (!string.IsNullOrEmpty(criterioFiltro) && !string.IsNullOrEmpty(valorBusqueda))
            {
                string val = valorBusqueda.Trim().ToLower();

                if (criterioFiltro == "ID" && int.TryParse(val, out int idVal))
                {
                    reunionesQuery = reunionesQuery.Where(r => r.ID_reunion == idVal);
                }
                else if (criterioFiltro == "Mes" && int.TryParse(val, out int mes))
                {
                    reunionesQuery = reunionesQuery.Where(r => r.fecha_reunion.Month == mes);
                }
                else if (criterioFiltro == "Fecha" && DateTime.TryParse(valorBusqueda, out DateTime fecha))
                {
                    reunionesQuery = reunionesQuery.Where(r => r.fecha_reunion == fecha);
                }
            }

            ViewBag.Reuniones = reunionesQuery.ToList();
            ViewBag.CriterioActual = criterioFiltro;
            ViewBag.ValorActual = valorBusqueda;

            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}