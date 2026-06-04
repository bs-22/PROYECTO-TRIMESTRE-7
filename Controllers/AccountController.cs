using System;
using System.Linq;
using System.Web.Mvc;
using GestionSemillero1.Models;

namespace GestionSemillero1.Controllers
{
    public class AccountController : Controller
    {
        // GET: Account/Login
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string Username, string Password)
        {
            try
            {
                // 1. CORRECCIÓN: Usamos DbSemillero en lugar de SemilleroContext
                using (var db = new DbSemillero())
                {
                    var usuario = db.Usuarios.FirstOrDefault(u =>
                        u.correo_usuario == Username &&
                        u.contraseña_usuario == Password);

                    if (usuario != null)
                    {
                        Session["UsuarioLogueado"] = usuario.correo_usuario;
                        Session["TipoUsuario"] = usuario.tipo_usuario;
                        Session["IDUsuario"] = usuario.ID_usuario;

                        string rol = usuario.tipo_usuario?.ToLower().Trim();

                        // 2. CORRECCIÓN: Adaptamos los roles a tu base de datos real
                        if (rol == "lider")
                        {
                            // Si entra Jeremias o Leizmy, van al panel de Lider
                            return RedirectToAction("Index", "Lider");
                        }
                        else if (rol == "administrador")
                        {
                            // Si entra Bryam, va al panel de Administrador
                            return RedirectToAction("Index", "Administrador");
                        }
                        else
                        {
                            ViewBag.Error = "Rol de usuario no reconocido.";
                            return View();
                        }
                    }
                    else
                    {
                        ViewBag.Error = "Correo o contraseña incorrectos.";
                        return View();
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error en el sistema: " + ex.Message;
                return View();
            }
        }
    }
}