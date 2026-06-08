using GestionSemillero1.Models;
using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;

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
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            try
            {
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

                        // EVALUACIÓN DE ROLES DESDE LA BASE DE DATOS
                        if (rol == "lider")
                        {
                            return RedirectToAction("Index", "Lider");
                        }
                        else if (rol == "administrador")
                        {
                            return RedirectToAction("Index", "Administrador");
                        }
                        // AGREGADO: Redirección correcta para el Investigador
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

        // GET: Account/Logout
        public ActionResult Logout()
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            Session.Clear();
            Session.Abandon();

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