using System.Web.Mvc;

namespace GestionSemillero1.Controllers
{
    public class AdministradorController : Controller
    {
        // GET: Investigador/Index
        public ActionResult Index()
        {
            // 1. VALIDACIÓN DE SEGURIDAD
            // Si no hay sesión activa o el usuario no es investigador, lo devolvemos al Login
            if (Session["UsuarioLogueado"] == null || Session["TipoUsuario"]?.ToString().ToLower() != "investigador")
            {
                return RedirectToAction("Login", "Account");
            }

            // 2. ENVIAR DATOS A LA VISTA
            // Podemos mandar el correo del usuario logueado a la vista usando ViewBag
            ViewBag.Usuario = Session["UsuarioLogueado"].ToString();

            return View();
        }
    }
}