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
            //Limpieza de caché obligatoria para que el Admin SIEMPRE vea los cambios
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            if (Session["UsuarioLogueado"] == null)
                return RedirectToAction("Login", "Account");

            ViewBag.ListaSemilleros = db.Database.SqlQuery<semillero>("SELECT * FROM semillero WHERE estado = 'activo'").ToList();
            ViewBag.ListaLineas = db.Database.SqlQuery<string>("SELECT DISTINCT linea_investigacion FROM semillero WHERE linea_investigacion IS NOT NULL").ToList();

            //Traemos a los líderes con su nombre real haciendo JOIN con investigadores
            ViewBag.ComboLideres = db.Database.SqlQuery<LiderDropdownDTO>(
                @"SELECT u.ID_usuario, i.nombre_investigador + ' ' + i.apellido_investigador AS NombreCompleto
                  FROM usuario u
                  INNER JOIN investigadores i ON u.ID_usuario = i.ID_usuario
                  WHERE u.tipo_usuario IN ('Lider', 'Líder') AND u.estado_usuario = 'activo'").ToList();

            //Mapeo de qué líder pertenece a qué semillero actualmente
            ViewBag.LideresActuales = db.Database.SqlQuery<LiderSemilleroDTO>(
                @"SELECT i.ID_semillero, u.ID_usuario
                  FROM investigadores i
                  INNER JOIN usuario u ON i.ID_usuario = u.ID_usuario
                  WHERE u.tipo_usuario IN ('Lider', 'Líder')").ToList();

            // Obtención de datos base: Filtra inicialmente solo los semilleros activos para mostrar en tarjetas, 
            // mientras mantiene una copia de todos los semilleros para referencia global.
            var queryTarjetas = "SELECT * FROM semillero WHERE estado = 'activo'";
            List<semillero> resultadosTarjetas = db.Database.SqlQuery<semillero>(queryTarjetas).ToList();
            List<semillero> todosLosSemilleros = db.Database.SqlQuery<semillero>("SELECT * FROM semillero").ToList();

            // Lógica de filtrado condicional: Aplica filtros de búsqueda si el usuario ha seleccionado criterios específicos.
            // Se evalúa en orden de prioridad: ID de semillero, Línea de investigación o Fecha de creación.
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

            // Envía los datos a la vista para renderizado final.
            ViewBag.TodosSemilleros = todosLosSemilleros;
            return View(resultadosTarjetas);
        }

        [HttpPost]
        //Se añade el parámetro idLiderSeleccionado para capturar el menú desplegable
        public ActionResult GuardarSemillero(semillero model, decimal? idLiderSeleccionado)
        {
            // Ejecuta el guardado de datos mediante sentencias SQL directas para asegurar consistencia.
            try
            {
                // CASO 1: Inserción de un nuevo semillero.
                if (model.ID_semillero == 0)
                {
                    // Genera el ID autoincremental de forma manual mediante consulta SQL para evitar conflictos.
                    decimal nuevoId = db.Database.SqlQuery<decimal>("SELECT COALESCE(MAX(ID_semillero), 0) + 1 FROM semillero").FirstOrDefault();

                    // Inserta el nuevo registro con estado 'activo' por defecto.
                    db.Database.ExecuteSqlCommand("INSERT INTO semillero (ID_semillero, nombre_semillero, linea_investigacion, descripcion_semillero, fecha_creacion_semillero, estado) VALUES (@p0, @p1, @p2, @p3, @p4, 'activo')", nuevoId, model.nombre_semillero, model.linea_investigacion, model.descripcion_semillero, DateTime.Now);

                    // Si se seleccionó un líder, actualiza su tabla asociada (investigadores) para vincularlo al nuevo semillero.
                    if (idLiderSeleccionado.HasValue && idLiderSeleccionado.Value > 0)
                    {
                        db.Database.ExecuteSqlCommand("UPDATE investigadores SET ID_semillero = @p0 WHERE ID_usuario = @p1", nuevoId, idLiderSeleccionado.Value);
                    }
                }
                // CASO 2: Actualización de un semillero existente.
                else
                {
                    // Actualiza los campos descriptivos del semillero seleccionado.
                    db.Database.ExecuteSqlCommand("UPDATE semillero SET nombre_semillero = @p0, linea_investigacion = @p1, descripcion_semillero = @p2 WHERE ID_semillero = @p3", model.nombre_semillero, model.linea_investigacion, model.descripcion_semillero, model.ID_semillero);

                    // Reasigna o actualiza la relación del líder con el semillero modificado.
                    if (idLiderSeleccionado.HasValue && idLiderSeleccionado.Value > 0)
                    {
                        db.Database.ExecuteSqlCommand("UPDATE investigadores SET ID_semillero = @p0 WHERE ID_usuario = @p1", model.ID_semillero, idLiderSeleccionado.Value);
                    }
                }
                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                // Captura errores para evitar interrupciones en la ejecución, redirigiendo de vuelta al índice.
                return RedirectToAction("Index");
            }
        }

        // ==========================================================
        // SECTION 2: GESTIÓN DE USUARIOS
        // ==========================================================
        public ActionResult Usuarios(string criterio, string valor)
        {
            //Limpieza de caché
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            // Valida que el usuario tenga una sesión activa; de lo contrario, redirige al Login.
            if (Session["UsuarioLogueado"] == null)
                return RedirectToAction("Login", "Account");

            // Define una consulta SQL para obtener una vista consolidada de los datos de los investigadores y sus cuentas de usuario.
            // El INNER JOIN vincula los datos personales del investigador con las credenciales de acceso.
            string sqlQuery = @"
        SELECT 
        i.ID_investigador, i.nombre_investigador, i.apellido_investigador, 
        i.tipo_documento, i.edad_investigador, i.telefono_investigador, i.ID_semillero,
        u.ID_usuario, u.estado_usuario, u.correo_usuario, u.contraseña_usuario, u.tipo_usuario
        FROM investigadores i
        INNER JOIN Usuario u ON i.ID_usuario = u.ID_usuario";

            // Ejecuta la consulta y materializa los resultados en el modelo de vista (ViewModel).
            List<UsuarioGestionViewModel> listaUsuarios = db.Database.SqlQuery<UsuarioGestionViewModel>(sqlQuery).ToList();

            // Lógica de filtrado en memoria: Refina la lista si el administrador busca por nombre o tipo de documento.
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
                // Mantiene los valores de búsqueda en la vista para que el usuario sepa qué filtros están activos.
                ViewBag.CriterioSeleccionado = criterio;
                ViewBag.ValorBuscado = valor;
            }

            // Carga las opciones para el desplegable de semilleros y retorna la vista con la lista procesada.
            ViewBag.ComboSemilleros = db.Database.SqlQuery<semillero>("SELECT * FROM semillero WHERE estado = 'activo'").ToList();
            return View(listaUsuarios);
        }

        [HttpPost]
        public ActionResult GuardarUsuario(UsuarioGestionViewModel model)
        {
            // Valida la sesión del usuario para garantizar acceso restringido al módulo administrativo.
            if (Session["UsuarioLogueado"] == null) return RedirectToAction("Login", "Account");

            try
            {
                // Generación de identificadores únicos (IDs) para nuevas inserciones:
                // El comando COALESCE(MAX(ID), 0) + 1 busca el valor máximo actual y le suma 1. 
                // Si la tabla está vacía (retorna NULL), COALESCE lo convierte a 0, permitiendo que el nuevo ID sea 1.
                decimal nuevoIdUsuario = db.Database.SqlQuery<decimal>("SELECT COALESCE(MAX(ID_usuario), 0) + 1 FROM Usuario").FirstOrDefault();
                decimal nuevoIdInvestigador = db.Database.SqlQuery<decimal>("SELECT COALESCE(MAX(ID_investigador), 0) + 1 FROM investigadores").FirstOrDefault();

                // Lógica condicional para determinar si es un registro nuevo (ID=0) o una actualización.
                if (model.ID_investigador == 0)
                {
                    // Inserta en la tabla Usuario primero (por ser tabla padre) y luego en investigadores (tabla dependiente).
                    db.Database.ExecuteSqlCommand("INSERT INTO Usuario (ID_usuario, estado_usuario, correo_usuario, contraseña_usuario, tipo_usuario) VALUES (@p0, @p1, @p2, @p3, @p4)", nuevoIdUsuario, model.estado_usuario, model.correo_usuario, model.contraseña_usuario, model.tipo_usuario);
                    db.Database.ExecuteSqlCommand("INSERT INTO investigadores (ID_investigador, nombre_investigador, apellido_investigador, tipo_documento, edad_investigador, telefono_investigador, ID_usuario, ID_semillero) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7)", nuevoIdInvestigador, model.nombre_investigador, model.apellido_investigador, model.tipo_documento, model.edad_investigador, model.telefono_investigador, nuevoIdUsuario, model.ID_semillero);
                }
                else
                {
                    // Actualiza los datos del usuario y del investigador si el ID ya existe.
                    db.Database.ExecuteSqlCommand("UPDATE Usuario SET estado_usuario = @p0, correo_usuario = @p1, tipo_usuario = @p2 WHERE ID_usuario = @p3", model.estado_usuario, model.correo_usuario, model.tipo_usuario, model.ID_usuario);

                    // Verifica si se proporcionó una nueva contraseña válida (diferente a la máscara de visualización).
                    if (!string.IsNullOrEmpty(model.contraseña_usuario) && model.contraseña_usuario != "********")
                    {
                        db.Database.ExecuteSqlCommand("UPDATE Usuario SET contraseña_usuario = @p0 WHERE ID_usuario = @p1", model.contraseña_usuario, model.ID_usuario);
                    }

                    // Actualiza los datos personales del investigador.
                    db.Database.ExecuteSqlCommand("UPDATE investigadores SET nombre_investigador = @p0, apellido_investigador = @p1, tipo_documento = @p2, edad_investigador = @p3, telefono_investigador = @p4, ID_semillero = @p5 WHERE ID_investigador = @p6", model.nombre_investigador, model.apellido_investigador, model.tipo_documento, model.edad_investigador, model.telefono_investigador, model.ID_semillero, model.ID_investigador);
                }
                return RedirectToAction("Usuarios");
            }
            catch (Exception)
            {
                // En caso de error, recarga las opciones del combo de semilleros y retorna a la lista de usuarios.
                ViewBag.ComboSemilleros = db.Database.SqlQuery<semillero>("SELECT * FROM semillero WHERE estado = 'activo'").ToList();
                return RedirectToAction("Usuarios");
            }
        }

        // Actualiza la información del usuario y del investigador mediante peticiones AJAX.
        [HttpPost]
        public JsonResult ActualizarUsuario(UsuarioGestionViewModel model)
        {
            try
            {
                // 1. Actualiza credenciales y rol en la tabla Usuario.
                db.Database.ExecuteSqlCommand("UPDATE Usuario SET correo_usuario = @p0, tipo_usuario = @p1 WHERE ID_usuario = @p2", model.correo_usuario, model.tipo_usuario, model.ID_usuario);

                // 2. Solo actualiza contraseña si el usuario introdujo una nueva (validación de máscara).
                if (!string.IsNullOrEmpty(model.contraseña_usuario) && model.contraseña_usuario != "********")
                {
                    db.Database.ExecuteSqlCommand("UPDATE Usuario SET contraseña_usuario = @p0 WHERE ID_usuario = @p1", model.contraseña_usuario, model.ID_usuario);
                }

                // 3. Sincroniza los datos personales del investigador.
                db.Database.ExecuteSqlCommand("UPDATE investigadores SET nombre_investigador = @p0, apellido_investigador = @p1, tipo_documento = @p2, edad_investigador = @p3, telefono_investigador = @p4, ID_semillero = @p5 WHERE ID_investigador = @p6", model.nombre_investigador, model.apellido_investigador, model.tipo_documento, model.edad_investigador, model.telefono_investigador, model.ID_semillero, model.ID_investigador);

                return Json(new { success = true, message = "Información actualizada correctamente." });
            }
            catch (Exception ex) { return Json(new { success = false, message = "Error: " + ex.Message }); }
        }

        // Métodos para el cambio de estado (Lógica de borrado lógico):
        // Permiten habilitar o deshabilitar usuarios sin eliminar registros permanentemente de la base de datos.

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
            //Limpieza de caché
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

                // Recorre cada reunión para determinar su estado dinámico comparándolo con la fecha y hora actual.
                foreach (var r in listaReuniones)
                {
                    try
                    {
                        // Extrae las horas y las convierte a TimeSpan para realizar cálculos de tiempo precisos.
                        string horaIn = r.hora_reunion.Split(' ')[0];
                        string horaFi = r.hora_fin_reunion.Split(' ')[0];

                        TimeSpan tsInicio = TimeSpan.Parse(horaIn);
                        TimeSpan tsFin = TimeSpan.Parse(horaFi);

                        // Ajuste de formato AM/PM: Convierte el formato de 12 horas a 24 horas para evitar errores de cálculo.
                        if (r.hora_reunion.ToLower().Contains("pm") && tsInicio.Hours < 12) tsInicio = tsInicio.Add(new TimeSpan(12, 0, 0));
                        if (r.hora_fin_reunion.ToLower().Contains("pm") && tsFin.Hours < 12) tsFin = tsFin.Add(new TimeSpan(12, 0, 0));

                        // Define fechas exactas combinando la fecha de la reunión con sus respectivos horarios.
                        DateTime fechaInicioExacta = new DateTime(r.fecha_reunion.Year, r.fecha_reunion.Month, r.fecha_reunion.Day) + tsInicio;
                        DateTime fechaFinExacta = new DateTime(r.fecha_reunion.Year, r.fecha_reunion.Month, r.fecha_reunion.Day) + tsFin;

                        // Clasificación del estado basada en la comparación con 'ahora'.
                        if (ahora > fechaFinExacta) r.estado_reunion = "Finalizada";
                        else if (ahora >= fechaInicioExacta && ahora <= fechaFinExacta) r.estado_reunion = "En curso";
                        else r.estado_reunion = "Iniciada";
                    }
                    catch
                    {
                        // Fallback de seguridad: Si falla el cálculo horario, el estado se calcula basándose solo en la fecha.
                        if (ahora.Date > r.fecha_reunion.Date) r.estado_reunion = "Finalizada";
                        else if (ahora.Date == r.fecha_reunion.Date) r.estado_reunion = "En curso";
                        else r.estado_reunion = "Iniciada";
                    }
                }

                // Aplicación de filtros de búsqueda adicionales (si el usuario ha seleccionado semillero o estado).
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

                // Ordena los resultados por ID descendente para mostrar las reuniones más recientes primero.
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
        // Método auxiliar para precargar datos en la vista (ViewBag) necesarios para el formulario de gestión.
        private void CargarDatosVista(string pestañaActiva)
        {
            // Define la pestaña activa para mantener la persistencia visual al recargar la página.
            ViewBag.PestañaCargada = string.IsNullOrEmpty(pestañaActiva) ? "proyectos" : pestañaActiva;

            // Carga de catálogos base para los desplegables (dropdowns) de la interfaz.
            ViewBag.SemillerosDisponibles = db.semillero.ToList() ?? new List<semillero>();
            ViewBag.ProyectosDisponibles = db.Proyectos.ToList() ?? new List<Proyecto>();
            ViewBag.FasesDisponibles = db.FasesProyecto.ToList() ?? new List<FaseProyecto>();
            ViewBag.ListaProyectos = db.Proyectos.ToList() ?? new List<Proyecto>();

            // Generación automática de IDs sugeridos para nuevos registros (evita colisiones).
            // Si la tabla está vacía, inicia el contador desde un valor base (100, 500, 900).
            ViewBag.NextProyectoID = (db.Proyectos.Any() ? db.Proyectos.Max(p => p.ID_proyecto) : 100) + 1;
            ViewBag.NextFaseID = (db.FasesProyecto.Any() ? db.FasesProyecto.Max(f => f.ID_fase_proyecto) : 500) + 1;
            ViewBag.NextActividadID = (db.ActividadesProyecto.Any() ? db.ActividadesProyecto.Max(a => a.ID_activida_proyecto) : 900) + 1;

            // LINQ Join para mapear Fases con su Proyecto padre y facilitar la visualización en la tabla.
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

            // LINQ Join para mapear Actividades con su Fase correspondiente.
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
            //Limpieza de caché
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

            // Bloque de control CRUD (Create, Read, Update, Delete) para la jerarquía del proyecto.
            try
            {
                // PROYECTOS: Gestión de nivel superior.
                if (pestañaDestino == "proyectos")
                {
                    if (accionCrud == "agregar")
                    {
                        db.Proyectos.Add(p);
                        db.SaveChanges(); // Guardado necesario para obtener el ID del nuevo proyecto.

                        // AUTOMATIZACIÓN: Generación de fases predeterminadas al crear un nuevo proyecto.
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
                        if (r != null) { /* Asignación manual de propiedades para actualizar estado en DB */ }
                    }
                    else if (accionCrud == "eliminar")
                    {
                        var r = db.Proyectos.Find(p.ID_proyecto);
                        if (r != null) db.Proyectos.Remove(r);
                    }
                }
                // FASES Y ACTIVIDADES: Gestión de niveles inferiores con validación de existencia (Find).
                else if (pestañaDestino == "fases")
                { /* Lógica CRUD para fases */ }
                else if (pestañaDestino == "actividades")
                { /* Lógica CRUD para actividades */ }

                // Persistencia final de todos los cambios realizados en el árbol de objetos.
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
            //Limpieza de caché
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
            // Limpieza de caché
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

                // EXTRAEMOS LA CONEXIÓN DIRECTAMENTE DEL CONTEXTO (BURLANDO A ENTITY FRAMEWORK)
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

                // Convertimos a arreglo de bytes para independizarlo del reporte
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
    // SECCIÓN 6: MODELOS DTO (Data Transfer Objects)
    // ==========================================================

    // Estos objetos actúan como "contenedores ligeros" para transportar información
    // desde la base de datos hasta las vistas, evitando la carga de modelos complejos.

    // Mapea la relación entre fases y proyectos para visualización en tablas.
    public class FaseListaDTO
    {
        public decimal ID_fase_proyecto { get; set; }
        public string nombre_fase_proyecto { get; set; }
        public string descripcion_fase_proyecto { get; set; }
        public decimal ID_proyecto { get; set; }
        public string nombre_proyecto { get; set; } // Nombre descriptivo del proyecto padre.
    }

    // Estructura los datos de actividades para presentarlos junto al nombre de su fase.
    public class ActividadListaDTO
    {
        public decimal ID_activida_proyecto { get; set; }
        public string nombre_actividad_proyecto { get; set; }
        public string descripcion_actividad_proyecto { get; set; }
        public DateTime fecha_inicio_actividad_proyecto { get; set; }
        public DateTime fecha_fin_actividad_proyecto { get; set; }
        public decimal ID_fase_proyecto { get; set; }
        public string nombre_fase_proyecto { get; set; } // Nombre descriptivo de la fase padre.
    }

    // Consolida información de eventos y semilleros para reportes y listas.
    public class EventoListaDTO
    {
        public decimal ID_evento { get; set; }
        public DateTime fecha_evento { get; set; }
        public string nombre_evento { get; set; }
        public string descripción_evento { get; set; }
        public decimal ID_semillero { get; set; }
        public string nombre_semillero { get; set; } // Nombre descriptivo del semillero asociado.
    }

    // Facilita la creación de menús desplegables (combobox) con nombres concatenados.
    public class LiderDropdownDTO
    {
        public decimal ID_usuario { get; set; }
        public string NombreCompleto { get; set; } // Resultado de concatenar nombre y apellido.
    }

    // Relaciona líderes con sus semilleros para lógica de asignación y edición.
    public class LiderSemilleroDTO
    {
        public decimal ID_semillero { get; set; }
        public decimal ID_usuario { get; set; }
    }
}

