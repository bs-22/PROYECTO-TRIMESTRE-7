using GestionSemillero1.Models;
using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace GestionSemillero1.Controllers
{
    public class AccountController : Controller
    {
        // ==========================================
        // MÓDULO DE AUTENTICACIÓN (LOGIN)
        // ==========================================

        // Renderiza la interfaz gráfica de inicio de sesión.
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        // Procesa las credenciales enviadas por el formulario.
        // Valida contra la base de datos, crea la sesión de usuario y redirige según el rol asignado.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string Username, string Password)
        {
            // Deshabilita la caché para evitar que el usuario vuelva atrás tras cerrar sesión.
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            try
            {
                using (var db = new DbSemillero())
                {
                    // Consulta el usuario en la base de datos comparando credenciales.
                    var usuario = db.Usuarios.FirstOrDefault(u =>
                        u.correo_usuario == Username &&
                        u.contraseña_usuario == Password);

                    if (usuario != null)
                    {
                        // Inicialización de variables de sesión globales.
                        Session["UsuarioLogueado"] = usuario.correo_usuario;
                        Session["TipoUsuario"] = usuario.tipo_usuario;
                        Session["IDUsuario"] = usuario.ID_usuario;

                        string rol = usuario.tipo_usuario?.ToLower().Trim();

                        // Enrutamiento de vistas basado en el rol del usuario autenticado.
                        if (rol == "lider")
                        {
                            return RedirectToAction("Index", "Lider");
                        }
                        else if (rol == "administrador")
                        {
                            return RedirectToAction("Index", "Administrador");
                        }
                        else if (rol == "investigador")
                        {
                            return RedirectToAction("Index", "Investigador");
                        }
                        else
                        {
                            ViewBag.Error = "Rol de usuario no reconocido.";
                            return View();
                        }
                    }
                    else
                    {
                        // Credenciales inválidas.
                        ViewBag.Error = "Correo o contraseña incorrectos.";
                        return View();
                    }
                }
            }
            catch (Exception ex)
            {
                // Captura de errores del sistema o conexión a la base de datos.
                ViewBag.Error = "Error en el sistema: " + ex.Message;
                return View();
            }
        }

        // ==========================================
        // MÓDULO DE CIERRE DE SESIÓN (LOGOUT)
        // ==========================================

        // Destruye la sesión actual, limpia las cookies de autenticación y devuelve al usuario al Login.
        [HttpGet]
        public ActionResult Logout()
        {
            // Limpieza de caché de navegación.
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            // Destrucción de variables de sesión en el servidor.
            Session.Clear();
            Session.Abandon();

            // Eliminación de la cookie de autenticación local.
            if (Request.Cookies[".ASPXAUTH"] != null)
            {
                HttpCookie cookie = new HttpCookie(".ASPXAUTH");
                cookie.Expires = DateTime.Now.AddDays(-1);
                Response.Cookies.Add(cookie);
            }

            return RedirectToAction("Login", "Account");
        }
    }
}
