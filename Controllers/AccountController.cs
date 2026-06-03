using System.Web.Mvc;

namespace GestionSemillero1.Controllers
{
    public class AccountController : Controller
    {
        // GET: Muestra la vista del Login al cargar la página
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        // POST: Recibe los datos cuando el usuario da clic en "Iniciar Sesión"
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string Username, string Password, bool RememberMe = false)
        {
            // Validación de prueba (Cambiar luego por tu base de datos)
            if (Username == "admin" && Password == "1234")
            {
                // Si es correcto, viaja al Index del Home (Dashboard)
                return RedirectToAction("Index", "Home");
            }

            // Si falla, guardamos el mensaje de error y recargamos la vista
            ViewBag.Error = "Usuario o contraseña incorrectos.";
            return View();
        }
    }
}