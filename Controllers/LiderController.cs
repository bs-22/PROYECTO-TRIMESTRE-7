using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace GestionSemillero1.Controllers
{
    public class LiderController : Controller
    {
        // GET: Lider/Index
        public ActionResult Index()
        {
            // Validación de seguridad: Solo entran líderes logueados
            if (Session["UsuarioLogueado"] == null || Session["TipoUsuario"]?.ToString().ToLower() != "lider")
            {
                return RedirectToAction("Login", "Account");
            }

            // Enviamos el correo a la vista
            ViewBag.Usuario = Session["UsuarioLogueado"].ToString();

            return View();
        }
    }
}