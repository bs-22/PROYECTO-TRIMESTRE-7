using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using GestionSemillero1.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;


namespace GestionSemillero1.Controllers
{
    public class AdministradorController : Controller
    {
        private DbSemillero db = new DbSemillero();

        // ==========================================================
        // SECTION 1: MENÚ PRINCIPAL Y SEMILLEROS
        // ==========================================================
        public ActionResult Index(decimal? filtroSemillero, string filtroLinea, string filtroFecha)
        {
            // 🌟 SOLUCIÓN: Limpieza de caché obligatoria para que el Admin SIEMPRE vea los cambios
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            if (Session["UsuarioLogueado"] == null)
                return RedirectToAction("Login", "Account");

            ViewBag.ListaSemilleros = db.Database.SqlQuery<semillero>("SELECT * FROM semillero WHERE estado = 'activo'").ToList();
            ViewBag.ListaLineas = db.Database.SqlQuery<string>("SELECT DISTINCT linea_investigacion FROM semillero WHERE linea_investigacion IS NOT NULL").ToList();

            // 🌟 MEJORA: Traemos a los líderes con su nombre real haciendo JOIN con investigadores
            ViewBag.ComboLideres = db.Database.SqlQuery<LiderDropdownDTO>(
                @"SELECT u.ID_usuario, i.nombre_investigador + ' ' + i.apellido_investigador AS NombreCompleto
                  FROM usuario u
                  INNER JOIN investigadores i ON u.ID_usuario = i.ID_usuario
                  WHERE u.tipo_usuario IN ('Lider', 'Líder') AND u.estado_usuario = 'activo'").ToList();

            // 🌟 NUEVO: Mapeo de qué líder pertenece a qué semillero actualmente
            ViewBag.LideresActuales = db.Database.SqlQuery<LiderSemilleroDTO>(
                @"SELECT i.ID_semillero, u.ID_usuario
                  FROM investigadores i
                  INNER JOIN usuario u ON i.ID_usuario = u.ID_usuario
                  WHERE u.tipo_usuario IN ('Lider', 'Líder')").ToList();

            var queryTarjetas = "SELECT * FROM semillero WHERE estado = 'activo'";
            List<semillero> resultadosTarjetas = db.Database.SqlQuery<semillero>(queryTarjetas).ToList();
            List<semillero> todosLosSemilleros = db.Database.SqlQuery<semillero>("SELECT * FROM semillero").ToList();

            if (filtroSemillero.HasValue)
            {
                resultadosTarjetas = resultadosTarjetas.Where(s => s.ID_semillero == filtroSemillero.Value).ToList();
                ViewBag.SemilleroSeleccionado = filtroSemillero;
            }
            else if (!string.IsNullOrEmpty(filtroLinea))
            {
                resultadosTarjetas = resultadosTarjetas.Where(s => s.linea_investigacion.ToLower() == filtroLinea.ToLower()).ToList();
                ViewBag.LineaSeleccionada = filtroLinea;
            }
            else if (!string.IsNullOrEmpty(filtroFecha))
            {
                DateTime fechaBusqueda = DateTime.Parse(filtroFecha);
                resultadosTarjetas = resultadosTarjetas.Where(s => s.fecha_creacion_semillero.Date == fechaBusqueda.Date).ToList();
                ViewBag.FechaSeleccionada = filtroFecha;
            }

            ViewBag.TodosSemilleros = todosLosSemilleros;
            return View(resultadosTarjetas);
        }

        [HttpPost]
        // 🌟 MEJORA: Se añade el parámetro idLiderSeleccionado para capturar el menú desplegable
        public ActionResult GuardarSemillero(semillero model, decimal? idLiderSeleccionado)
        {
            try
            {
                if (model.ID_semillero == 0)
                {
                    decimal nuevoId = db.Database.SqlQuery<decimal>("SELECT COALESCE(MAX(ID_semillero), 0) + 1 FROM semillero").FirstOrDefault();
                    db.Database.ExecuteSqlCommand("INSERT INTO semillero (ID_semillero, nombre_semillero, linea_investigacion, descripcion_semillero, fecha_creacion_semillero, estado) VALUES (@p0, @p1, @p2, @p3, @p4, 'activo')", nuevoId, model.nombre_semillero, model.linea_investigacion, model.descripcion_semillero, DateTime.Now);

                    // Asignar líder al nuevo semillero
                    if (idLiderSeleccionado.HasValue && idLiderSeleccionado.Value > 0)
                    {
                        db.Database.ExecuteSqlCommand("UPDATE investigadores SET ID_semillero = @p0 WHERE ID_usuario = @p1", nuevoId, idLiderSeleccionado.Value);
                    }
                }
                else
                {
                    db.Database.ExecuteSqlCommand("UPDATE semillero SET nombre_semillero = @p0, linea_investigacion = @p1, descripcion_semillero = @p2 WHERE ID_semillero = @p3", model.nombre_semillero, model.linea_investigacion, model.descripcion_semillero, model.ID_semillero);

                    // Mover al líder seleccionado a este semillero editado
                    if (idLiderSeleccionado.HasValue && idLiderSeleccionado.Value > 0)
                    {
                        db.Database.ExecuteSqlCommand("UPDATE investigadores SET ID_semillero = @p0 WHERE ID_usuario = @p1", model.ID_semillero, idLiderSeleccionado.Value);
                    }
                }
                return RedirectToAction("Index");
            }
            catch (Exception) { return RedirectToAction("Index"); }
        }

        // ==========================================================
        // SECTION 2: GESTIÓN DE USUARIOS
        // ==========================================================
        public ActionResult Usuarios(string criterio, string valor)
        {
            // 🌟 SOLUCIÓN: Limpieza de caché
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            if (Session["UsuarioLogueado"] == null)
                return RedirectToAction("Login", "Account");

            string sqlQuery = @"
                SELECT 
                    i.ID_investigador, i.nombre_investigador, i.apellido_investigador, 
                    i.tipo_documento, i.edad_investigador, i.telefono_investigador, i.ID_semillero,
                    u.ID_usuario, u.estado_usuario, u.correo_usuario, u.contraseña_usuario AS contraseña_usuario, u.tipo_usuario
                FROM investigadores i
                INNER JOIN Usuario u ON i.ID_usuario = u.ID_usuario";

            List<UsuarioGestionViewModel> listaUsuarios = db.Database.SqlQuery<UsuarioGestionViewModel>(sqlQuery).ToList();

            if (!string.IsNullOrEmpty(criterio) && !string.IsNullOrEmpty(valor))
            {
                valor = valor.ToLower().Trim();
                if (criterio == "nombre")
                {
                    listaUsuarios = listaUsuarios.Where(u => u.nombre_investigador.ToLower().Contains(valor) || u.apellido_investigador.ToLower().Contains(valor)).ToList();
                }
                else if (criterio == "documento")
                {
                    listaUsuarios = listaUsuarios.Where(u => u.tipo_documento.ToLower() == valor).ToList();
                }
                ViewBag.CriterioSeleccionado = criterio;
                ViewBag.ValorBuscado = valor;
            }

            ViewBag.ComboSemilleros = db.Database.SqlQuery<semillero>("SELECT * FROM semillero WHERE estado = 'activo'").ToList();
            return View(listaUsuarios);
        }

        [HttpPost]
        public ActionResult GuardarUsuario(UsuarioGestionViewModel model)
        {
            if (Session["UsuarioLogueado"] == null) return RedirectToAction("Login", "Account");
            try
            {
                decimal nuevoIdUsuario = db.Database.SqlQuery<decimal>("SELECT COALESCE(MAX(ID_usuario), 0) + 1 FROM Usuario").FirstOrDefault();
                decimal nuevoIdInvestigador = db.Database.SqlQuery<decimal>("SELECT COALESCE(MAX(ID_investigador), 0) + 1 FROM investigadores").FirstOrDefault();

                if (model.ID_investigador == 0)
                {
                    db.Database.ExecuteSqlCommand("INSERT INTO Usuario (ID_usuario, estado_usuario, correo_usuario, contraseña_usuario, tipo_usuario) VALUES (@p0, @p1, @p2, @p3, @p4)", nuevoIdUsuario, model.estado_usuario, model.correo_usuario, model.contraseña_usuario, model.tipo_usuario);
                    db.Database.ExecuteSqlCommand("INSERT INTO investigadores (ID_investigador, nombre_investigador, apellido_investigador, tipo_documento, edad_investigador, telefono_investigador, ID_usuario, ID_semillero) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7)", nuevoIdInvestigador, model.nombre_investigador, model.apellido_investigador, model.tipo_documento, model.edad_investigador, model.telefono_investigador, nuevoIdUsuario, model.ID_semillero);
                }
                else
                {
                    db.Database.ExecuteSqlCommand("UPDATE Usuario SET estado_usuario = @p0, correo_usuario = @p1, tipo_usuario = @p2 WHERE ID_usuario = @p3", model.estado_usuario, model.correo_usuario, model.tipo_usuario, model.ID_usuario);
                    if (!string.IsNullOrEmpty(model.contraseña_usuario) && model.contraseña_usuario != "********")
                    {
                        db.Database.ExecuteSqlCommand("UPDATE Usuario SET contraseña_usuario = @p0 WHERE ID_usuario = @p1", model.contraseña_usuario, model.ID_usuario);
                    }
                    db.Database.ExecuteSqlCommand("UPDATE investigadores SET nombre_investigador = @p0, apellido_investigador = @p1, tipo_documento = @p2, edad_investigador = @p3, telefono_investigador = @p4, ID_semillero = @p5 WHERE ID_investigador = @p6", model.nombre_investigador, model.apellido_investigador, model.tipo_documento, model.edad_investigador, model.telefono_investigador, model.ID_semillero, model.ID_investigador);
                }
                return RedirectToAction("Usuarios");
            }
            catch (Exception)
            {
                ViewBag.ComboSemilleros = db.Database.SqlQuery<semillero>("SELECT * FROM semillero WHERE estado = 'activo'").ToList();
                return RedirectToAction("Usuarios");
            }
        }

        [HttpPost]
        public JsonResult ActualizarUsuario(UsuarioGestionViewModel model)
        {
            try
            {
                db.Database.ExecuteSqlCommand("UPDATE Usuario SET correo_usuario = @p0, tipo_usuario = @p1 WHERE ID_usuario = @p2", model.correo_usuario, model.tipo_usuario, model.ID_usuario);
                if (!string.IsNullOrEmpty(model.contraseña_usuario) && model.contraseña_usuario != "********")
                {
                    db.Database.ExecuteSqlCommand("UPDATE Usuario SET contraseña_usuario = @p0 WHERE ID_usuario = @p1", model.contraseña_usuario, model.ID_usuario);
                }
                db.Database.ExecuteSqlCommand("UPDATE investigadores SET nombre_investigador = @p0, apellido_investigador = @p1, tipo_documento = @p2, edad_investigador = @p3, telefono_investigador = @p4, ID_semillero = @p5 WHERE ID_investigador = @p6", model.nombre_investigador, model.apellido_investigador, model.tipo_documento, model.edad_investigador, model.telefono_investigador, model.ID_semillero, model.ID_investigador);
                return Json(new { success = true, message = "Información actualizada correctamente." });
            }
            catch (Exception ex) { return Json(new { success = false, message = "Error: " + ex.Message }); }
        }

        [HttpPost]
        public JsonResult DeshabilitarUsuario(decimal idUsuario)
        {
            try { db.Database.ExecuteSqlCommand("UPDATE Usuario SET estado_usuario = 'Inactivo' WHERE ID_usuario = @p0", idUsuario); return Json(new { success = true, message = "Cuenta deshabilitada." }); }
            catch (Exception ex) { return Json(new { success = false, message = "Error: " + ex.Message }); }
        }

        [HttpPost]
        public JsonResult HabilitarUsuario(decimal idUsuario)
        {
            try { db.Database.ExecuteSqlCommand("UPDATE Usuario SET estado_usuario = 'Activo' WHERE ID_usuario = @p0", idUsuario); return Json(new { success = true, message = "Cuenta habilitada." }); }
            catch (Exception ex) { return Json(new { success = false, message = "Error: " + ex.Message }); }
        }

        // ==========================================================
        // SECTION 3: REUNIONES
        // ==========================================================
        public ActionResult Reuniones(decimal? filtroSemillero, string filtroEstado)
        {
            // 🌟 SOLUCIÓN: Limpieza de caché
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            if (Session["UsuarioLogueado"] == null)
                return RedirectToAction("Login", "Account");

            try
            {
                ViewBag.ListaSemilleros = db.Database.SqlQuery<semillero>("SELECT * FROM semillero WHERE estado = 'activo'").ToList();
                ViewBag.ListaUsuarios = db.Database.SqlQuery<Usuario>("SELECT * FROM usuario WHERE estado_usuario = 'activo'").ToList();

                string query = @"
                    SELECT 
                        ID_reunion, 
                        descripcion_reunion, 
                        hora_reunion, 
                        hora_fin_reunion, 
                        lugar_reunion, 
                        fecha_reunion, 
                        ID_semillero, 
                        estado_reunion
                    FROM reunion";

                List<Reunion> listaReuniones = db.Database.SqlQuery<Reunion>(query).ToList();
                DateTime ahora = DateTime.Now;

                foreach (var r in listaReuniones)
                {
                    try
                    {
                        string horaIn = r.hora_reunion.Split(' ')[0];
                        string horaFi = r.hora_fin_reunion.Split(' ')[0];

                        TimeSpan tsInicio = TimeSpan.Parse(horaIn);
                        TimeSpan tsFin = TimeSpan.Parse(horaFi);

                        if (r.hora_reunion.ToLower().Contains("pm") && tsInicio.Hours < 12) tsInicio = tsInicio.Add(new TimeSpan(12, 0, 0));
                        if (r.hora_fin_reunion.ToLower().Contains("pm") && tsFin.Hours < 12) tsFin = tsFin.Add(new TimeSpan(12, 0, 0));

                        DateTime fechaInicioExacta = new DateTime(r.fecha_reunion.Year, r.fecha_reunion.Month, r.fecha_reunion.Day) + tsInicio;
                        DateTime fechaFinExacta = new DateTime(r.fecha_reunion.Year, r.fecha_reunion.Month, r.fecha_reunion.Day) + tsFin;

                        if (ahora > fechaFinExacta) r.estado_reunion = "Finalizada";
                        else if (ahora >= fechaInicioExacta && ahora <= fechaFinExacta) r.estado_reunion = "En curso";
                        else r.estado_reunion = "Iniciada";
                    }
                    catch
                    {
                        if (ahora.Date > r.fecha_reunion.Date) r.estado_reunion = "Finalizada";
                        else if (ahora.Date == r.fecha_reunion.Date) r.estado_reunion = "En curso";
                        else r.estado_reunion = "Iniciada";
                    }
                }

                if (filtroSemillero.HasValue && filtroSemillero.Value > 0)
                {
                    listaReuniones = listaReuniones.Where(r => r.ID_semillero == filtroSemillero.Value).ToList();
                    ViewBag.SemilleroSel = filtroSemillero;
                }

                if (!string.IsNullOrEmpty(filtroEstado))
                {
                    string estadoBuscado = filtroEstado.Trim().ToLower();
                    listaReuniones = listaReuniones.Where(r => r.estado_reunion.Trim().ToLower() == estadoBuscado).ToList();
                    ViewBag.EstadoSel = filtroEstado;
                }

                listaReuniones = listaReuniones.OrderByDescending(r => r.ID_reunion).ToList();
                return View(listaReuniones);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error: " + ex.Message);
                return View(new List<Reunion>());
            }
        }

        [HttpGet]
        public JsonResult ObtenerParticipantes(decimal idReunion)
        {
            string sql = "SELECT ID_usuario FROM asistencia_reunion WHERE ID_reunion = @p0";
            var ids = db.Database.SqlQuery<decimal>(sql, idReunion).ToList();
            return Json(ids, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult GuardarReunion(FormCollection form, List<decimal> usuariosAsignados)
        {
            if (Session["UsuarioLogueado"] == null) return RedirectToAction("Login", "Account");

            if (string.IsNullOrEmpty(form["descripcion_reunion"]) || string.IsNullOrEmpty(form["fecha_reunion"]) ||
                string.IsNullOrEmpty(form["hora_reunion"]) || string.IsNullOrEmpty(form["lugar_reunion"]))
            {
                ModelState.AddModelError("", "Todos los campos son obligatorios.");
                return RedirectToAction("Reuniones");
            }

            try
            {
                string idReunionStr = form["ID_reunion"];
                string descripcion = form["descripcion_reunion"];
                string horaInicio = form["hora_reunion"];
                string horaFin = form["hora_fin_reunion"];
                string lugar = form["lugar_reunion"];
                string fechaStr = form["fecha_reunion"];
                string idSemilleroStr = form["ID_semillero"];
                string estado = form["estado_reunion"] ?? "Iniciada";

                decimal idReunion = 0;
                decimal.TryParse(idReunionStr, out idReunion);
                DateTime fecha = DateTime.Parse(fechaStr);

                if (fecha.Date < DateTime.Today || fecha.DayOfWeek == DayOfWeek.Sunday)
                {
                    ModelState.AddModelError("", "Error en restricciones de fecha (Días pasados o Domingos).");
                    return RedirectToAction("Reuniones");
                }

                decimal? idSemillero = null;
                if (!string.IsNullOrEmpty(idSemilleroStr))
                {
                    if (decimal.TryParse(idSemilleroStr, out decimal idSemVal)) idSemillero = idSemVal;
                }

                if (idReunion == 0)
                {
                    decimal nuevoId = db.Database.SqlQuery<decimal>("SELECT COALESCE(MAX(ID_reunion), 0) + 1 FROM reunion").FirstOrDefault();
                    string sqlInsert = @"INSERT INTO reunion (ID_reunion, descripcion_reunion, hora_reunion, hora_fin_reunion, lugar_reunion, fecha_reunion, ID_semillero, estado_reunion) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7)";
                    db.Database.ExecuteSqlCommand(sqlInsert, nuevoId, descripcion, horaInicio, horaFin, lugar, fecha, idSemillero, estado);

                    if (usuariosAsignados != null)
                    {
                        foreach (var idUsuario in usuariosAsignados)
                            db.Database.ExecuteSqlCommand("INSERT INTO asistencia_reunion (ID_reunion, ID_usuario) VALUES (@p0, @p1)", nuevoId, idUsuario);
                    }
                }
                else
                {
                    string sqlUpdate = @"UPDATE reunion SET descripcion_reunion = @p0, hora_reunion = @p1, hora_fin_reunion = @p2, lugar_reunion = @p3, fecha_reunion = @p4, ID_semillero = @p5, estado_reunion = @p6 WHERE ID_reunion = @p7";
                    db.Database.ExecuteSqlCommand(sqlUpdate, descripcion, horaInicio, horaFin, lugar, fecha, idSemillero, estado, idReunion);

                    db.Database.ExecuteSqlCommand("DELETE FROM asistencia_reunion WHERE ID_reunion = @p0", idReunion);
                    if (usuariosAsignados != null)
                    {
                        foreach (var idUsuario in usuariosAsignados)
                            db.Database.ExecuteSqlCommand("INSERT INTO asistencia_reunion (ID_reunion, ID_usuario) VALUES (@p0, @p1)", idReunion, idUsuario);
                    }
                }
                return RedirectToAction("Reuniones");
            }
            catch (Exception) { return RedirectToAction("Reuniones"); }
        }

        [HttpPost]
        public JsonResult EliminarReunion(decimal idReunion)
        {
            try
            {
                db.Database.ExecuteSqlCommand("DELETE FROM reunion WHERE ID_reunion = @p0", idReunion);
                return Json(new { success = true, message = "Reunión eliminada correctamente del sistema." });
            }
            catch (Exception ex) { return Json(new { success = false, message = "Error: " + ex.Message }); }
        }

        // ==========================================================
        // SECTION 4: GESTIÓN DE PROYECTOS, FASES Y ACTIVIDADES
        // ==========================================================
        private void CargarDatosVista(string pestañaActiva)
        {
            ViewBag.PestañaCargada = string.IsNullOrEmpty(pestañaActiva) ? "proyectos" : pestañaActiva;

            ViewBag.SemillerosDisponibles = db.semillero.ToList() ?? new List<semillero>();
            ViewBag.ProyectosDisponibles = db.Proyectos.ToList() ?? new List<Proyecto>();
            ViewBag.FasesDisponibles = db.FasesProyecto.ToList() ?? new List<FaseProyecto>();
            ViewBag.ListaProyectos = db.Proyectos.ToList() ?? new List<Proyecto>();

            ViewBag.NextProyectoID = (db.Proyectos.Any() ? db.Proyectos.Max(p => p.ID_proyecto) : 100) + 1;
            ViewBag.NextFaseID = (db.FasesProyecto.Any() ? db.FasesProyecto.Max(f => f.ID_fase_proyecto) : 500) + 1;
            ViewBag.NextActividadID = (db.ActividadesProyecto.Any() ? db.ActividadesProyecto.Max(a => a.ID_activida_proyecto) : 900) + 1;

            ViewBag.ListaFases = (from f in db.FasesProyecto
                                  join p in db.Proyectos on f.ID_proyecto equals p.ID_proyecto into joined
                                  from p in joined.DefaultIfEmpty()
                                  select new FaseListaDTO
                                  {
                                      ID_fase_proyecto = f.ID_fase_proyecto,
                                      nombre_fase_proyecto = f.nombre_fase_proyecto,
                                      descripcion_fase_proyecto = f.descripcion_fase_proyecto,
                                      ID_proyecto = f.ID_proyecto,
                                      nombre_proyecto = p != null ? p.nombre_proyecto : "Sin proyecto"
                                  }).ToList();

            ViewBag.ListaActividades = (from a in db.ActividadesProyecto
                                        join f in db.FasesProyecto on a.ID_fase_proyecto equals f.ID_fase_proyecto into joined
                                        from f in joined.DefaultIfEmpty()
                                        select new ActividadListaDTO
                                        {
                                            ID_activida_proyecto = a.ID_activida_proyecto,
                                            nombre_actividad_proyecto = a.nombre_actividad_proyecto,
                                            descripcion_actividad_proyecto = a.descripcion_actividad_proyecto,
                                            fecha_inicio_actividad_proyecto = a.fecha_inicio_actividad_proyecto,
                                            fecha_fin_actividad_proyecto = a.fecha_fin_actividad_proyecto,
                                            ID_fase_proyecto = a.ID_fase_proyecto,
                                            nombre_fase_proyecto = f != null ? f.nombre_fase_proyecto : "Sin fase"
                                        }).ToList();
        }

        [HttpGet]
        public ActionResult RegistrarProyecto(string pestañaActiva = "proyectos")
        {
            // 🌟 SOLUCIÓN: Limpieza de caché
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            if (Session["UsuarioLogueado"] == null) return RedirectToAction("Login", "Account");
            CargarDatosVista(pestañaActiva);
            return View();
        }

        [HttpPost]
        public ActionResult RegistrarProyecto(Proyecto p, FaseProyecto f, ActividadProyecto a, string tipoSubmit, string accionCrud)
        {
            if (Session["UsuarioLogueado"] == null) return RedirectToAction("Login", "Account");
            string pestañaDestino = string.IsNullOrEmpty(tipoSubmit) ? "proyectos" : tipoSubmit;

            try
            {
                if (pestañaDestino == "proyectos")
                {
                    if (accionCrud == "agregar")
                    {
                        db.Proyectos.Add(p);
                        db.SaveChanges();

                        var nombresFases = new List<string> { "Análisis y Arquitectura", "Desarrollo de Vistas", "Casos de Prueba", "Configuración Docker", "Recolección de Datos" };
                        var descripcionesFases = new List<string> { "Definición del modelo relacional MER y endpoints", "Creación de componentes y estilos de la interfaz UI", "Diseño y ejecución de pruebas funcionales y de estrés", "Creación de contenedores y orquestación del entorno", "Limpieza y estructuración de datos para el semillero" };

                        decimal ultimoIdFase = db.FasesProyecto.Any() ? db.FasesProyecto.Max(x => x.ID_fase_proyecto) : 500;

                        for (int i = 0; i < nombresFases.Count; i++)
                        {
                            ultimoIdFase++;
                            var faseAutomatica = new FaseProyecto
                            {
                                ID_fase_proyecto = ultimoIdFase,
                                ID_proyecto = p.ID_proyecto,
                                nombre_fase_proyecto = nombresFases[i],
                                descripcion_fase_proyecto = descripcionesFases[i]
                            };
                            db.FasesProyecto.Add(faseAutomatica);
                        }
                    }
                    else if (accionCrud == "actualizar")
                    {
                        var r = db.Proyectos.Find(p.ID_proyecto);
                        if (r != null)
                        {
                            r.nombre_proyecto = p.nombre_proyecto; r.actividad_proyecto = p.actividad_proyecto;
                            r.fecha_inicio_proyecto = p.fecha_inicio_proyecto; r.fecha_fin_proyecto = p.fecha_fin_proyecto;
                            r.ID_semillero = p.ID_semillero;
                        }
                    }
                    else if (accionCrud == "eliminar")
                    {
                        var r = db.Proyectos.Find(p.ID_proyecto);
                        if (r != null) db.Proyectos.Remove(r);
                    }
                }
                else if (pestañaDestino == "fases")
                {
                    if (accionCrud == "agregar") db.FasesProyecto.Add(f);
                    else if (accionCrud == "actualizar")
                    {
                        var r = db.FasesProyecto.Find(f.ID_fase_proyecto);
                        if (r != null) { r.nombre_fase_proyecto = f.nombre_fase_proyecto; r.descripcion_fase_proyecto = f.descripcion_fase_proyecto; r.ID_proyecto = f.ID_proyecto; }
                    }
                    else if (accionCrud == "eliminar")
                    {
                        var r = db.FasesProyecto.Find(f.ID_fase_proyecto);
                        if (r != null) db.FasesProyecto.Remove(r);
                    }
                }
                else if (pestañaDestino == "actividades")
                {
                    if (accionCrud == "agregar") db.ActividadesProyecto.Add(a);
                    else if (accionCrud == "actualizar")
                    {
                        var r = db.ActividadesProyecto.Find(a.ID_activida_proyecto);
                        if (r != null)
                        {
                            r.nombre_actividad_proyecto = a.nombre_actividad_proyecto; r.descripcion_actividad_proyecto = a.descripcion_actividad_proyecto;
                            r.fecha_inicio_actividad_proyecto = a.fecha_inicio_actividad_proyecto; r.fecha_fin_actividad_proyecto = a.fecha_fin_actividad_proyecto;
                            r.ID_fase_proyecto = a.ID_fase_proyecto;
                        }
                    }
                    else if (accionCrud == "eliminar")
                    {
                        var r = db.ActividadesProyecto.Find(a.ID_activida_proyecto);
                        if (r != null) db.ActividadesProyecto.Remove(r);
                    }
                }

                db.SaveChanges();
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                TempData["ErrorProyecto"] = "Error: " + innerException;
            }

            return RedirectToAction("RegistrarProyecto", new { pestañaActiva = pestañaDestino });
        }

        // ==========================================================
        // SECTION 5: GESTIÓN DE EVENTOS
        // ==========================================================
        public ActionResult RegistrarEvento(string criterio, string valor, string fechaFiltro)
        {
            // 🌟 SOLUCIÓN: Limpieza de caché
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            if (Session["UsuarioLogueado"] == null)
                return RedirectToAction("Login", "Account");

            ViewBag.SemillerosDisponibles = db.Database.SqlQuery<semillero>("SELECT * FROM semillero WHERE estado = 'activo'").ToList();
            ViewBag.NextEventoID = db.Database.SqlQuery<decimal>("SELECT COALESCE(MAX(ID_evento), 0) + 1 FROM evento").FirstOrDefault();

            string query = @"
                SELECT 
                    e.ID_evento, 
                    e.fecha_evento, 
                    e.nombre_evento, 
                    e.descripción_evento, 
                    e.ID_semillero, 
                    s.nombre_semillero
                FROM evento e
                INNER JOIN semillero s ON e.ID_semillero = s.ID_semillero";

            List<EventoListaDTO> listaEventos = db.Database.SqlQuery<EventoListaDTO>(query).ToList();

            if (!string.IsNullOrEmpty(criterio) && !string.IsNullOrEmpty(valor))
            {
                valor = valor.ToLower().Trim();
                if (criterio == "nombre") listaEventos = listaEventos.Where(e => e.nombre_evento.ToLower().Contains(valor)).ToList();
                else if (criterio == "semillero") listaEventos = listaEventos.Where(e => e.nombre_semillero.ToLower().Contains(valor)).ToList();
                ViewBag.CriterioSeleccionado = criterio;
                ViewBag.ValorBuscado = valor;
            }

            if (!string.IsNullOrEmpty(fechaFiltro) && DateTime.TryParse(fechaFiltro, out DateTime fBusqueda))
            {
                listaEventos = listaEventos.Where(e => e.fecha_evento.Date == fBusqueda.Date).ToList();
                ViewBag.FechaFiltrada = fechaFiltro;
            }

            return View(listaEventos.OrderBy(e => e.ID_evento).ToList());
        }

        [HttpPost]
        public ActionResult GuardarEvento(Evento model, string accionCrud)
        {
            if (Session["UsuarioLogueado"] == null) return RedirectToAction("Login", "Account");

            try
            {
                if (accionCrud == "agregar" || accionCrud == "actualizar")
                {
                    if (string.IsNullOrWhiteSpace(model.nombre_evento) || string.IsNullOrWhiteSpace(model.descripción_evento) || model.ID_semillero == 0)
                    {
                        TempData["ErrorEvento"] = "Validación fallida: Todos los campos obligatorios deben estar diligenciados.";
                        return RedirectToAction("RegistrarEvento");
                    }

                    if (model.fecha_evento.Date < DateTime.Today)
                    {
                        TempData["ErrorEvento"] = "Validación fallida: La fecha seleccionada ya pasó.";
                        return RedirectToAction("RegistrarEvento");
                    }
                }

                if (accionCrud == "agregar")
                {
                    decimal nuevoId = db.Database.SqlQuery<decimal>("SELECT COALESCE(MAX(ID_evento), 0) + 1 FROM evento").FirstOrDefault();
                    db.Database.ExecuteSqlCommand("INSERT INTO evento (ID_evento, fecha_evento, nombre_evento, descripción_evento, ID_semillero) VALUES (@p0, @p1, @p2, @p3, @p4)", nuevoId, model.fecha_evento, model.nombre_evento, model.descripción_evento, model.ID_semillero);
                    TempData["ExitoEvento"] = "¡Excelente! El evento ha sido programado.";
                }
                else if (accionCrud == "actualizar")
                {
                    db.Database.ExecuteSqlCommand("UPDATE evento SET fecha_evento = @p0, nombre_evento = @p1, descripción_evento = @p2, ID_semillero = @p3 WHERE ID_evento = @p4", model.fecha_evento, model.nombre_evento, model.descripción_evento, model.ID_semillero, model.ID_evento);
                    TempData["ExitoEvento"] = "¡Perfecto! El evento fue modificado.";
                }
                else if (accionCrud == "eliminar")
                {
                    db.Database.ExecuteSqlCommand("DELETE FROM evento WHERE ID_evento = @p0", model.ID_evento);
                    TempData["ExitoEvento"] = "El evento ha sido eliminado.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorEvento"] = "Error: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message);
            }

            return RedirectToAction("RegistrarEvento");
        }

        //SECCION DE REPORTES 
        // 1. GET: Administrador/Reportes
        public ActionResult Reportes()
        {
            // 🌟 SOLUCIÓN: Limpieza de caché
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            using (var db = new DbSemillero())
            {
                ViewBag.TotalSemilleros = db.semillero.Count();
                ViewBag.TotalUsuarios = db.Usuarios.Count();
            }
            return View();
        }

        [HttpPost]
        public ActionResult GenerarCrystalReport(string nombreReporte)
        {
            ReportDocument reportDocument = new ReportDocument();
            try
            {
                string rutaReporte = Path.Combine(Server.MapPath("~/Reports"), nombreReporte + ".rpt");

                if (!System.IO.File.Exists(rutaReporte))
                {
                    return Content("Error: El archivo físico " + nombreReporte + ".rpt no existe.");
                }

                reportDocument.Load(rutaReporte);

                // 🔥 EXTRAEMOS LA CONEXIÓN DIRECTAMENTE DEL CONTEXTO (BURLANDO A ENTITY FRAMEWORK)
                string stringConexion = new DbSemillero().Database.Connection.ConnectionString;

                using (System.Data.SqlClient.SqlConnection conn = new System.Data.SqlClient.SqlConnection(stringConexion))
                {
                    string querySQL = "";

                    // Definimos la consulta plana dependiendo del reporte
                    switch (nombreReporte)
                    {
                        // --- ADMIN ---
                        case "Reporte_Investigadores_General":
                            querySQL = "SELECT * FROM investigadores";
                            break;
                        case "Reporte_Lideres_General":
                            // Filtramos usando SQL puro
                            querySQL = @"SELECT i.* FROM investigadores i 
                                 INNER JOIN Usuario u ON i.ID_usuario = u.ID_usuario 
                                 WHERE u.tipo_usuario IN ('Lider', 'Administrador', 'Líder')";
                            break;
                        case "Reporte_Semilleros_Lineas":
                            querySQL = "SELECT * FROM semillero";
                            break;
                        case "Reporte_Proyectos_Eventos":
                            querySQL = "SELECT * FROM proyecto";
                            break;
                        case "Reporte_Eventos_General":
                            querySQL = "SELECT * FROM evento";
                            break;

                        // --- LÍDER ---
                        case "Reporte_Mis_Investigadores":
                            querySQL = "SELECT * FROM investigadores";
                            break;
                        case "Reporte_Mis_Proyectos":
                            querySQL = "SELECT * FROM proyecto";
                            break;
                        case "Reporte_Avance_Fases":
                            querySQL = "SELECT * FROM FasesProyecto";
                            break;
                        case "Reporte_Reuniones_Por_Semillero":
                            querySQL = "SELECT * FROM reunion";
                            break;
                        case "Reporte_Mis_Eventos":
                            querySQL = "SELECT * FROM evento";
                            break;
                    }

                    // Si hay una consulta válida, llenamos la tabla plana y se la pasamos a Crystal
                    if (!string.IsNullOrEmpty(querySQL))
                    {
                        System.Data.SqlClient.SqlDataAdapter adaptador = new System.Data.SqlClient.SqlDataAdapter(querySQL, conn);
                        System.Data.DataTable tablaPlana = new System.Data.DataTable();
                        adaptador.Fill(tablaPlana);

                        // Le pasamos la tabla pura a Crystal. ¡Cero bucles infinitos!
                        reportDocument.SetDataSource(tablaPlana);
                    }
                }

                // 🌟 LA MAGIA ESTÁ AQUÍ: Convertimos a arreglo de bytes para independizarlo del reporte
                Stream stream = reportDocument.ExportToStream(ExportFormatType.PortableDocFormat);
                byte[] byteArray;
                using (MemoryStream ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    byteArray = ms.ToArray();
                }

                // Retornamos el File usando los bytes, no el stream vivo de Crystal
                return File(byteArray, "application/pdf", nombreReporte + ".pdf");
            }
            catch (Exception ex)
            {
                return Content("Error crítico al generar reporte: " + ex.Message);
            }
            finally
            {
                // Limpieza EXTREMA para evitar colapso en el segundo intento
                if (reportDocument != null)
                {
                    try
                    {
                        // 1. Limpiamos conexiones de base de datos internas del reporte
                        if (reportDocument.DataSourceConnections != null)
                        {
                            reportDocument.DataSourceConnections.Clear();
                        }

                        // 2. Cerramos el documento explícitamente
                        reportDocument.Close();

                        // 3. Liberamos recursos no administrados (C++)
                        reportDocument.Dispose();
                    }
                    catch
                    {
                        // Si falla limpiando, ignoramos para no tumbar el servidor
                    }
                }
            }
        }
    }

    // ==========================================================
    // SECTION 6: MODELOS DTO / VIEWMODELS PARA VISTAS 
    // ==========================================================
    public class FaseListaDTO
    {
        public decimal ID_fase_proyecto { get; set; }
        public string nombre_fase_proyecto { get; set; }
        public string descripcion_fase_proyecto { get; set; }
        public decimal ID_proyecto { get; set; }
        public string nombre_proyecto { get; set; }
    }

    public class ActividadListaDTO
    {
        public decimal ID_activida_proyecto { get; set; }
        public string nombre_actividad_proyecto { get; set; }
        public string descripcion_actividad_proyecto { get; set; }
        public DateTime fecha_inicio_actividad_proyecto { get; set; }
        public DateTime fecha_fin_actividad_proyecto { get; set; }
        public decimal ID_fase_proyecto { get; set; }
        public string nombre_fase_proyecto { get; set; }
    }

    public class EventoListaDTO
    {
        public decimal ID_evento { get; set; }
        public DateTime fecha_evento { get; set; }
        public string nombre_evento { get; set; }
        public string descripción_evento { get; set; }
        public decimal ID_semillero { get; set; }
        public string nombre_semillero { get; set; }
    }

    public class LiderDropdownDTO
    {
        public decimal ID_usuario { get; set; }
        public string NombreCompleto { get; set; }
    }

    public class LiderSemilleroDTO
    {
        public decimal ID_semillero { get; set; }
        public decimal ID_usuario { get; set; }
    }
}

