using GestionSemillero1.Models;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CrystalDecisions.CrystalReports.Engine; // Obligatorio aquí arriba
using CrystalDecisions.Shared;

namespace GestionSemillero1.Controllers
{
    public class LiderController : Controller
    {
        private DbSemillero db = new DbSemillero();
        private const string LAYOUT_PATH = "~/Views/Shared/_Layout.cshtml";

        // =========================================================================
        // GESTIONAR REUNIONES
        // =========================================================================
        public ActionResult Index()
        {
            if (Session["IDUsuario"] == null) return RedirectToAction("Login", "Account");

            decimal idUsuarioActual = Convert.ToDecimal(Session["IDUsuario"]);

            // 1. Obtener semillero
            var idSemilleroLider = db.investigadores
                                    .Where(i => i.ID_usuario == idUsuarioActual)
                                    .Select(i => i.ID_semillero)
                                    .FirstOrDefault();

            // 2. Reuniones (vía ViewBag para no chocar con el modelo de investigadores)
            ViewBag.ReunionesSemillero = (from r in db.Reunion
                                          where r.ID_semillero == idSemilleroLider ||
                                                db.AsistenciaReunion.Any(a => a.ID_reunion == r.ID_reunion && a.ID_usuario == idUsuarioActual)
                                          select r).Distinct().ToList();

            // 3. Investigadores (ESTE es el @model que la vista espera)
            var listaInvestigadores = db.investigadores
                                       .Where(i => i.ID_semillero == idSemilleroLider)
                                       .ToList();

            // RETORNO SEGURO: Si lista es null, enviamos una nueva lista vacía
            return View(listaInvestigadores ?? new List<GestionSemillero1.Models.investigadores>());
        }

        // Metodo api que devuelve los IDs de los usuarios que asisten a una reunión específica
        [HttpGet]
        public JsonResult ObtenerAsistentesReunion(decimal idReunion)
        {
            var asistentesIds = db.AsistenciaReunion
                                  .Where(a => a.ID_reunion == idReunion)
                                  .Select(a => a.ID_usuario.ToString())
                                  .ToList();

            return Json(asistentesIds, JsonRequestBehavior.AllowGet);
        }


        [HttpGet]
        public JsonResult VerificarDisponibilidad(DateTime fecha, string horaInicio, string horaFin, int idReunionActual = 0)
        {
            var reunionesConflictivas = db.Reunion
                .Where(r => r.fecha_reunion == fecha
                         && r.hora_reunion == horaInicio
                         && r.ID_reunion != idReunionActual)
                .Select(r => r.ID_reunion)
                .ToList();

            var usuariosOcupados = db.AsistenciaReunion
                .Where(a => reunionesConflictivas.Contains(a.ID_reunion))
                .Select(a => a.ID_usuario)
                .Distinct()
                .ToList();

            return Json(usuariosOcupados, JsonRequestBehavior.AllowGet);
        }

        // =========================================================================
        // CREAR O ACTUALIZAR REUNIÓN 
        // =========================================================================
        [HttpPost]
        public ActionResult ProcesarReunion(FormCollection form, decimal[] investigadoresSeleccionados)
        {
            if (Session["IDUsuario"] == null) return RedirectToAction("Login", "Account");
            decimal idLider = Convert.ToDecimal(Session["IDUsuario"]);

            try
            {
                string idRaw = form["ID_reunion"];
                string descripcion_reunion = form["descripcion_reunion"];
                string hora_reunion = form["hora_reunion"];
                string hora_fin_reunion = form["hora_fin_reunion"];
                string lugar_reunion = form["lugar_reunion"];
                string fecha_reunion = form["fecha_reunion"];

                // Convertimos y añadimos parseo inteligente que entiende AM/PM y 24h sin crashear
                DateTime fecha = DateTime.Parse(fecha_reunion);
                TimeSpan hIni = DateTime.Parse(hora_reunion).TimeOfDay;
                TimeSpan hFin = DateTime.Parse(hora_fin_reunion).TimeOfDay;

                // Validar Rango permitido obligatoriamente de 6 AM a 6 PM
                TimeSpan limiteApertura = new TimeSpan(6, 0, 0);
                TimeSpan limiteCierre = new TimeSpan(18, 0, 0);

                if (hIni < limiteApertura || hIni > limiteCierre || hFin < limiteApertura || hFin > limiteCierre)
                {
                    TempData["Error"] = "Operación Rechazada: El rango de horario permitido es de 06:00 a 18:00.";
                    return RedirectToAction("Index");
                }

                using (var transaccion = db.Database.BeginTransaction())
                {
                    try
                    {
                        if (string.IsNullOrEmpty(idRaw))
                        {
                            // =========================================================
                            // AGENDAR NUEVA REUNIÓN
                            // =========================================================

                            if (investigadoresSeleccionados != null)
                            {
                                foreach (var idInv in investigadoresSeleccionados)
                                {
                                    var inv = db.investigadores.FirstOrDefault(i => i.ID_investigador == idInv);
                                    if (inv != null)
                                    {
                                        var reunionesAsignadas = (from a in db.AsistenciaReunion
                                                                  join r in db.Reunion on a.ID_reunion equals r.ID_reunion
                                                                  where a.ID_usuario == inv.ID_usuario && r.fecha_reunion == fecha
                                                                  select r).ToList();

                                        foreach (var r in reunionesAsignadas)
                                        {
                                            //Evitar que falle al leer la BD
                                            TimeSpan dbIni = DateTime.Parse(r.hora_reunion.ToString()).TimeOfDay;
                                            TimeSpan dbFin = DateTime.Parse(r.hora_fin_reunion.ToString()).TimeOfDay;

                                            if ((hIni >= dbIni && hIni < dbFin) || (hFin > dbIni && hFin <= dbFin) || (hIni <= dbIni && hFin >= dbFin))
                                            {
                                                throw new Exception($"El participante {inv.nombre_investigador} no está disponible en ese horario.");
                                            }
                                        }
                                    }
                                }
                            }

                            decimal siguienteId = 5001;
                            var maxId = db.Database.SqlQuery<decimal?>("SELECT MAX(ID_reunion) FROM reunion").FirstOrDefault();
                            if (maxId.HasValue) siguienteId = maxId.Value + 1;

                            string sqlInsert = "INSERT INTO reunion (ID_reunion, descripcion_reunion, hora_reunion, hora_fin_reunion, lugar_reunion, fecha_reunion, ID_semillero, estado_reunion) " +
                                               "VALUES (@id, @desc, @hini, @hfin, @lug, @fec, 2001, 'Programada')";

                            db.Database.ExecuteSqlCommand(sqlInsert,
                                new System.Data.SqlClient.SqlParameter("@id", siguienteId),
                                new System.Data.SqlClient.SqlParameter("@desc", descripcion_reunion),
                                new System.Data.SqlClient.SqlParameter("@hini", hora_reunion),
                                new System.Data.SqlClient.SqlParameter("@hfin", hora_fin_reunion),
                                new System.Data.SqlClient.SqlParameter("@lug", lugar_reunion),
                                new System.Data.SqlClient.SqlParameter("@fec", fecha));

                            InsertarAsistencia(siguienteId, idLider);

                            if (investigadoresSeleccionados != null)
                            {
                                foreach (var idInv in investigadoresSeleccionados)
                                {
                                    var inv = db.investigadores.FirstOrDefault(i => i.ID_investigador == idInv);
                                    if (inv != null)
                                    {
                                        var usuarioReal = db.Usuarios.FirstOrDefault(u => u.ID_usuario == inv.ID_usuario);
                                        if (usuarioReal != null && usuarioReal.ID_usuario != idLider)
                                        {
                                            InsertarAsistencia(siguienteId, usuarioReal.ID_usuario);
                                        }
                                    }
                                }
                            }
                            TempData["Success"] = "La reunión ha sido agendada correctamente.";
                        }
                        else
                        {
                            // =========================================================
                            // MODO: ACTUALIZAR REUNIÓN EXISTENTE
                            // =========================================================
                            decimal idModificar = Convert.ToDecimal(idRaw);
                            var reunionExistente = db.Reunion.FirstOrDefault(r => r.ID_reunion == idModificar);

                            if (reunionExistente == null) throw new Exception("Reunión no encontrada.");

                            // Conversión segura al verificar cambios de hora
                            TimeSpan dbHoraIni = DateTime.Parse(reunionExistente.hora_reunion.ToString()).TimeOfDay;

                            if (reunionExistente.fecha_reunion != fecha && (reunionExistente.fecha_reunion - DateTime.Now.Date).TotalDays < 1)
                            {
                                throw new Exception("Los cambios de fecha requieren 24 horas de anticipación.");
                            }
                            if (dbHoraIni != hIni && (reunionExistente.fecha_reunion.Add(dbHoraIni) - DateTime.Now).TotalHours < 1)
                            {
                                throw new Exception("Los cambios de horario exigen mínimo 1 hora de anticipación.");
                            }

                            // Actualizar datos del acta
                            string sqlUpdate = "UPDATE reunion SET descripcion_reunion=@desc, hora_reunion=@hini, hora_fin_reunion=@hfin, lugar_reunion=@lug, fecha_reunion=@fec WHERE ID_reunion=@id";
                            db.Database.ExecuteSqlCommand(sqlUpdate,
                                new System.Data.SqlClient.SqlParameter("@desc", descripcion_reunion),
                                new System.Data.SqlClient.SqlParameter("@hini", hora_reunion),
                                new System.Data.SqlClient.SqlParameter("@hfin", hora_fin_reunion),
                                new System.Data.SqlClient.SqlParameter("@lug", lugar_reunion),
                                new System.Data.SqlClient.SqlParameter("@fec", fecha),
                                new System.Data.SqlClient.SqlParameter("@id", idModificar));

                            
                            // 1. Borramos a todos los asignados (excepto al líder dueño)
                            string sqlClearAsistencias = "DELETE FROM asistencia_reunion WHERE ID_reunion=@id AND ID_usuario != @idLider";
                            db.Database.ExecuteSqlCommand(sqlClearAsistencias,
                                new System.Data.SqlClient.SqlParameter("@id", idModificar),
                                new System.Data.SqlClient.SqlParameter("@idLider", idLider));

                            // 2. Insertamos a los nuevos que seleccionaste en los checkboxes
                            if (investigadoresSeleccionados != null)
                            {
                                foreach (var idInv in investigadoresSeleccionados)
                                {
                                    var inv = db.investigadores.FirstOrDefault(i => i.ID_investigador == idInv);
                                    if (inv != null)
                                    {
                                        var usuarioReal = db.Usuarios.FirstOrDefault(u => u.ID_usuario == inv.ID_usuario);
                                        if (usuarioReal != null && usuarioReal.ID_usuario != idLider)
                                        {
                                            InsertarAsistencia(idModificar, usuarioReal.ID_usuario);
                                        }
                                    }
                                }
                            }

                            TempData["Success"] = "Participantes y detalles de la reunión actualizados con éxito.";
                        }

                        transaccion.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaccion.Rollback();
                        TempData["Error"] = ex.Message;
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error de procesamiento: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // =========================================================================
        // ELIMINAR REUNIÓN FÍSICAMENTE
        // =========================================================================
        [HttpPost]
        public ActionResult EliminarReunion(FormCollection form)
        {
            string idRaw = form["ID_reunion"];
            if (string.IsNullOrEmpty(idRaw)) return RedirectToAction("Index");

            decimal idEliminar = Convert.ToDecimal(idRaw);

            using (var transaccion = db.Database.BeginTransaction())
            {
                try
                {
                    // 1. Limpiamos las dependencias asociativas de la tabla puente
                    string sqlAsistencias = "DELETE FROM asistencia_reunion WHERE ID_reunion = @id";
                    db.Database.ExecuteSqlCommand(sqlAsistencias, new System.Data.SqlClient.SqlParameter("@id", idEliminar));

                    // 2. Removemos el registro físico de la tabla reunión
                    string sqlReunion = "DELETE FROM reunion WHERE ID_reunion = @id";
                    db.Database.ExecuteSqlCommand(sqlReunion, new System.Data.SqlClient.SqlParameter("@id", idEliminar));

                    transaccion.Commit();
                    TempData["Success"] = "La reunión ha sido removida permanentemente del sistema institucional.";
                }
                catch (Exception ex)
                {
                    transaccion.Rollback();
                    TempData["Error"] = "Error de Base de Datos al eliminar: " + ex.Message;
                }
            }

            return RedirectToAction("Index");
        }

        // Helper interno parametrizado para registrar las asistencias asociadas
        private void InsertarAsistencia(decimal idR, decimal idU)
        {
            string sql = "INSERT INTO asistencia_reunion (ID_reunion, ID_usuario) VALUES (@idR, @idU)";
            db.Database.ExecuteSqlCommand(sql,
                new System.Data.SqlClient.SqlParameter("@idR", idR),
                new System.Data.SqlClient.SqlParameter("@idU", idU));
        }

        // =========================================================================
        // GET: Lider/ConsultarReuniones
        // =========================================================================
        public ActionResult ConsultarReuniones(string criterio, string valor)
        {
            // Validación de seguridad: Verifica sesión activa.
            if (Session["IDUsuario"] == null) return RedirectToAction("Login", "Account");

            decimal idUsuarioActual = Convert.ToDecimal(Session["IDUsuario"]);

            // Identificación del contexto: Obtiene el ID del semillero que lidera el usuario actual.
            var idSemilleroLider = db.investigadores
                                     .Where(i => i.ID_usuario == idUsuarioActual)
                                     .Select(i => i.ID_semillero)
                                     .FirstOrDefault();

            // Manejo de casos sin semillero: Retorna listas vacías si el líder no tiene semillero asignado.
            if (idSemilleroLider == 0)
            {
                ViewBag.TopIDs = new List<string>();
                ViewBag.TopFechas = new List<string>();
                return View(new List<Reunion>());
            }

            // Precarga de equipo: Obtiene todos los investigadores pertenecientes a este semillero.
            ViewBag.ListaInvestigadores = db.investigadores.Where(i => i.ID_semillero == idSemilleroLider).ToList();

            // Consulta de alcance (Scope): Filtra reuniones creadas para el semillero O reuniones donde el líder es asistente.
            var queryBase = (from r in db.Reunion
                             where r.ID_semillero == idSemilleroLider ||
                                   db.AsistenciaReunion.Any(a => a.ID_reunion == r.ID_reunion && a.ID_usuario == idUsuarioActual)
                             select r).Distinct();

            // Precarga de filtros (ViewBag): Prepara los 5 IDs y fechas más recientes para los buscadores rápidos.
            ViewBag.TopIDs = queryBase.Select(r => r.ID_reunion.ToString()).Distinct().Take(5).ToList();
            var fechasRaw = queryBase.Select(r => r.fecha_reunion).Distinct().Take(5).ToList();
            ViewBag.TopFechas = fechasRaw.AsEnumerable().Select(f => f.ToString("dd/MM/yyyy")).Where(x => !string.IsNullOrEmpty(x)).ToList();

            // Lógica de filtrado dinámico: Aplica filtros de búsqueda si el usuario ingresó criterios.
            var reuniones = queryBase.AsQueryable();

            if (!string.IsNullOrWhiteSpace(valor))
            {
                if (criterio == "ID" && decimal.TryParse(valor, out decimal id))
                {
                    reuniones = reuniones.Where(r => r.ID_reunion == id);
                }
                else if (criterio == "Fecha")
                {
                    // Intenta el parseo exacto para evitar conflictos de formato regional (dd/MM/yyyy).
                    if (DateTime.TryParseExact(valor, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime fechaExacta))
                    {
                        reuniones = reuniones.Where(r => r.fecha_reunion == fechaExacta);
                    }
                    else if (DateTime.TryParse(valor, out DateTime fecha))
                    {
                        reuniones = reuniones.Where(r => r.fecha_reunion == fecha);
                    }
                }
            }

            return View(reuniones.ToList());
        }

        // MÉTODOS AUXILIARES
        // Método de utilidad (Helper) para encapsular la creación y adición de parámetros a un comando SQL.
        // Esto centraliza la lógica de vinculación, mejorando la seguridad y legibilidad.
        private void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
        {
            // Crea un nuevo parámetro específico para el tipo de proveedor de datos (SQL, Oracle, etc.).
            var p = cmd.CreateParameter();

            // Asigna el nombre del parámetro (ej: "@p0").
            p.ParameterName = name;

            // Asigna el valor del objeto.
            p.Value = value;

            // Vincula el parámetro al comando SQL para su ejecución segura.
            cmd.Parameters.Add(p);
        }

        // =========================================================================
        // GESTIONAR SEMILLERO 
        // =========================================================================
        public ActionResult GestionarSemillero()
        {
            // Limpieza de caché para evitar que datos antiguos interfieran en la gestión actual.
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            // Seguridad: Restringe acceso solo a usuarios con sesión activa y rol de "lider".
            if (Session["UsuarioLogueado"] == null || Session["TipoUsuario"]?.ToString().ToLower() != "lider")
            {
                return RedirectToAction("Login", "Account");
            }

            string valorSesion = Session["UsuarioLogueado"].ToString();
            decimal idLiderActual = 0;

            // Resolución de identidad: Obtiene el ID del líder, ya sea de sesión o mediante consulta si el valor es un correo.
            if (!decimal.TryParse(valorSesion, out idLiderActual))
            {
                var usuarioDb = db.Usuarios.FirstOrDefault(u => u.correo_usuario == valorSesion);
                if (usuarioDb != null) idLiderActual = usuarioDb.ID_usuario;
                else return RedirectToAction("Login", "Account");
            }

            // Consulta de semilleros: Obtiene todos los semilleros vinculados al ID del líder.
            var misSemilleros = (from s in db.semillero
                                 join i in db.investigadores on s.ID_semillero equals i.ID_semillero
                                 where i.ID_usuario == idLiderActual
                                 select s).Distinct().ToList() ?? new List<semillero>();

            // Extracción de IDs para el filtrado de investigadores.
            var idsMisSemilleros = misSemilleros.Select(s => s.ID_semillero).ToList();

            // Precarga de equipo: Trae todos los investigadores asociados a los semilleros del líder.
            // Excluye al propio líder de la lista de gestión para evitar auto-modificaciones.
            ViewBag.InvestigadoresDeEsteSemillero = (from i in db.investigadores
                                                     join u in db.Usuarios on i.ID_usuario equals u.ID_usuario
                                                     where idsMisSemilleros.Contains(i.ID_semillero) && i.ID_usuario != idLiderActual
                                                     select new InvestigadorFiltroViewModel
                                                     {
                                                         ID_investigador = i.ID_investigador,
                                                         NombreCompleto = i.nombre_investigador + " " + i.apellido_investigador,
                                                         correo_usuario = u.correo_usuario,
                                                         estado_usuario = u.estado_usuario,
                                                         ID_usuario = i.ID_usuario
                                                     }).ToList();

            return View(misSemilleros);
        }

        // Acción POST para persistir actualizaciones o eliminaciones de semilleros.
        [HttpPost]
        public ActionResult GestionarSemillero(semillero datosSemillero, string accion)
        {
            // Validación de sesión: Garantiza que solo usuarios autenticados realicen modificaciones.
            if (Session["UsuarioLogueado"] == null) return RedirectToAction("Login", "Account");

            // Identificación del usuario: Obtiene el ID del líder para validar permisos.
            string valorSesion = Session["UsuarioLogueado"].ToString();
            decimal idLiderActual = 0;
            if (!decimal.TryParse(valorSesion, out idLiderActual))
            {
                var usuarioDb = db.Usuarios.FirstOrDefault(u => u.correo_usuario == valorSesion);
                if (usuarioDb != null) { idLiderActual = usuarioDb.ID_usuario; }
                else { return RedirectToAction("Login", "Account"); }
            }

            try
            {
                if (accion == "actualizar" || accion == "eliminar")
                {
                    // SEGURIDAD: Verifica que el semillero pertenezca realmente al líder actual.
                    var registro = (from s in db.semillero
                                    join i in db.investigadores on s.ID_semillero equals i.ID_semillero
                                    where s.ID_semillero == datosSemillero.ID_semillero && i.ID_usuario == idLiderActual
                                    select s).FirstOrDefault();

                    if (registro != null)
                    {
                        if (accion == "actualizar")
                        {
                            // Actualización: Aplica cambios a los campos editables.
                            registro.nombre_semillero = datosSemillero.nombre_semillero;
                            registro.linea_investigacion = datosSemillero.linea_investigacion;
                            registro.descripcion_semillero = datosSemillero.descripcion_semillero;
                            TempData["Success"] = "La información del semillero ha sido actualizada exitosamente.";
                        }
                        else if (accion == "eliminar")
                        {
                            // Eliminación en cascada manual: Remueve primero los miembros asociados para evitar errores de restricción de clave foránea.
                            var miembros = db.investigadores.Where(i => i.ID_semillero == datosSemillero.ID_semillero);
                            db.investigadores.RemoveRange(miembros);
                            db.semillero.Remove(registro);
                            TempData["Success"] = "El semillero ha sido eliminado.";
                        }
                        db.SaveChanges(); // Consolida los cambios en la base de datos.
                    }
                    else
                    {
                        // Validación de privilegios: Si el registro no se encuentra, significa que el usuario no tiene permisos de edición.
                        TempData["Error"] = "No cuenta con los privilegios requeridos para alterar este registro.";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error inesperado al procesar la solicitud: " + ex.Message;
            }

            return RedirectToAction("GestionarSemillero");
        }


        [HttpPost]
        public ActionResult ProcesarAccionesInvestigadores(decimal? idSemilleroSeleccionado, string idUsuarioSeleccionado, string accionInvestigador, string nuevoInvestigadorNombre, string nuevoInvestigadorApellido, string nuevoInvestigadorDoc, int? nuevoInvestigadorEdad, decimal? nuevoInvestigadorTel, string nuevoInvestigadorCorreo, string nuevoInvestigadorContrasena)
        {
            if (Session["UsuarioLogueado"] == null) return RedirectToAction("Login", "Account");

            string valorSesion = Session["UsuarioLogueado"].ToString();
            decimal idLiderActual = 0;
            if (!decimal.TryParse(valorSesion, out idLiderActual))
            {
                var usuarioDb = db.Usuarios.FirstOrDefault(u => u.correo_usuario == valorSesion);
                if (usuarioDb != null) { idLiderActual = usuarioDb.ID_usuario; }
                else { return RedirectToAction("Login", "Account"); }
            }

            bool esDueno = (from s in db.semillero
                            join i in db.investigadores on s.ID_semillero equals i.ID_semillero
                            where s.ID_semillero == idSemilleroSeleccionado && i.ID_usuario == idLiderActual
                            select s).Any();

            if (!esDueno)
            {
                TempData["Error"] = "Operación bloqueada. No administra el semillero de destino.";
                return RedirectToAction("GestionarSemillero");
            }

            if (accionInvestigador == "agregar")
            {
                using (var transaccion = db.Database.BeginTransaction())
                {
                    try
                    {
                        var correoLimpio = nuevoInvestigadorCorreo.Trim().ToLower();
                        if (db.Usuarios.Any(u => u.correo_usuario.Trim().ToLower() == correoLimpio))
                            throw new Exception("El correo electrónico ya se encuentra en uso dentro del sistema.");

                        Usuario nuevoUsuario = new Usuario
                        {
                            ID_usuario = (db.Usuarios.Max(u => (decimal?)u.ID_usuario) ?? 0) + 1,
                            correo_usuario = nuevoInvestigadorCorreo.Trim(),
                            contraseña_usuario = nuevoInvestigadorContrasena,
                            tipo_usuario = "investigador",
                            estado_usuario = "activo"
                        };
                        db.Usuarios.Add(nuevoUsuario);
                        db.SaveChanges();

                        investigadores nuevoInv = new investigadores
                        {
                            ID_investigador = (db.investigadores.Max(i => (decimal?)i.ID_investigador) ?? 0) + 1,
                            nombre_investigador = nuevoInvestigadorNombre,
                            apellido_investigador = nuevoInvestigadorApellido,
                            tipo_documento = nuevoInvestigadorDoc,
                            edad_investigador = nuevoInvestigadorEdad ?? 20,
                            telefono_investigador = nuevoInvestigadorTel ?? 0,
                            ID_usuario = nuevoUsuario.ID_usuario,
                            ID_semillero = idSemilleroSeleccionado.Value
                        };
                        db.investigadores.Add(nuevoInv);
                        db.SaveChanges();

                        transaccion.Commit();
                        TempData["Success"] = "Investigador registrado exitosamente.";
                    }
                    catch (Exception ex) { transaccion.Rollback(); TempData["Error"] = ex.Message; }
                }
            }
            else
            {
                try
                {
                    decimal idUser = Convert.ToDecimal(idUsuarioSeleccionado);
                    var usuario = db.Usuarios.Find(idUser);
                    if (usuario != null)
                    {
                        if (accionInvestigador == "habilitar")
                        {
                            usuario.estado_usuario = "activo";
                            TempData["Success"] = "Usuario habilitado correctamente.";
                        }
                        else if (accionInvestigador == "deshabilitar")
                        {
                            usuario.estado_usuario = "inactivo";
                            TempData["Success"] = "Usuario deshabilitado correctamente.";
                        }
                        else if (accionInvestigador == "eliminar")
                        {
                            var relacion = db.investigadores.FirstOrDefault(i => i.ID_usuario == idUser && i.ID_semillero == idSemilleroSeleccionado.Value);
                            if (relacion != null) db.investigadores.Remove(relacion);
                            TempData["Success"] = "Usuario removido del semillero.";
                        }
                        db.SaveChanges();
                    }
                }
                catch (Exception ex) { TempData["Error"] = ex.Message; }
            }

            return RedirectToAction("GestionarSemillero");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        // =========================================================================
        // REGISTRAR PROYECTO
        // =========================================================================

        // --- 1. GET: RegistrarProyecto ---
        public ActionResult RegistrarProyecto(string pestañaActiva)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            if (Session["UsuarioLogueado"] == null || Session["TipoUsuario"]?.ToString().ToLower() != "lider")
            {
                return RedirectToAction("Login", "Account");
            }

            CargarDatosVista(pestañaActiva);
            return View();
        }

        // --- 2. POST: RegistrarProyecto ---
        // Acción centralizada para realizar operaciones CRUD (Crear, Leer, Actualizar, Borrar) 
        // sobre la jerarquía: Proyectos -> Fases -> Actividades.
        [HttpPost]
        public ActionResult RegistrarProyecto(Proyecto p, FaseProyecto f, ActividadProyecto a, string tipoSubmit, string accionCrud)
        {
            // Seguridad: Verificación de sesión activa.
            if (Session["UsuarioLogueado"] == null) return RedirectToAction("Login", "Account");

            // Determina el módulo de destino (tabulación) basado en la acción del usuario.
            string pestañaDestino = string.IsNullOrEmpty(tipoSubmit) ? "proyectos" : tipoSubmit;

            try
            {
                // GESTIÓN DE PROYECTOS: Nivel superior.
                if (pestañaDestino == "proyecto")
                {
                    pestañaDestino = "proyectos";
                    if (accionCrud == "agregar") { db.Proyectos.Add(p); }
                    else if (accionCrud == "actualizar")
                    {
                        var r = db.Proyectos.Find(p.ID_proyecto);
                        if (r != null) { r.nombre_proyecto = p.nombre_proyecto; r.actividad_proyecto = p.actividad_proyecto; r.fecha_inicio_proyecto = p.fecha_inicio_proyecto; r.fecha_fin_proyecto = p.fecha_fin_proyecto; r.ID_semillero = p.ID_semillero; }
                    }
                    else if (accionCrud == "eliminar")
                    {
                        var r = db.Proyectos.Find(p.ID_proyecto);
                        if (r != null) db.Proyectos.Remove(r);
                    }
                    db.SaveChanges();
                }
                // GESTIÓN DE FASES: Nivel intermedio.
                else if (pestañaDestino == "fase")
                {
                    pestañaDestino = "fases";
                    if (accionCrud == "agregar") { db.FasesProyecto.Add(f); }
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
                    db.SaveChanges();
                }
                // GESTIÓN DE ACTIVIDADES: Nivel detallado.
                else if (pestañaDestino == "actividad")
                {
                    pestañaDestino = "actividades";
                    if (accionCrud == "agregar") { db.ActividadesProyecto.Add(a); }
                    else if (accionCrud == "actualizar")
                    {
                        var r = db.ActividadesProyecto.Find(a.ID_activida_proyecto);
                        if (r != null) { r.nombre_actividad_proyecto = a.nombre_actividad_proyecto; r.descripcion_actividad_proyecto = a.descripcion_actividad_proyecto; r.fecha_inicio_actividad_proyecto = a.fecha_inicio_actividad_proyecto; r.fecha_fin_actividad_proyecto = a.fecha_fin_actividad_proyecto; r.ID_fase_proyecto = a.ID_fase_proyecto; }
                    }
                    else if (accionCrud == "eliminar")
                    {
                        var r = db.ActividadesProyecto.Find(a.ID_activida_proyecto);
                        if (r != null) db.ActividadesProyecto.Remove(r);
                    }
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorProyecto"] = "Error en base de datos: " + ex.Message;
            }

            // Refresca los datos en la vista para mantener el estado de la pestaña seleccionada.
            CargarDatosVista(pestañaDestino);
            return View();
        }

        // --- 3. HELPER: CargarDatosVista ---
        // Método auxiliar para precargar datos en el ViewBag, filtrando exclusivamente 
        // por el semillero asociado al líder que ha iniciado sesión.
        private void CargarDatosVista(string pestañaActiva)
        {
            // Define la pestaña activa para mantener la persistencia visual.
            ViewBag.PestañaCargada = string.IsNullOrEmpty(pestañaActiva) ? "proyectos" : pestañaActiva;

            // Identificación: Obtiene el ID del semillero asignado al líder actual para realizar los filtros.
            decimal idUsuarioActual = Convert.ToDecimal(Session["IDUsuario"]);
            var idSemilleroLider = db.investigadores
                                     .Where(i => i.ID_usuario == idUsuarioActual)
                                     .Select(i => i.ID_semillero)
                                     .FirstOrDefault();

            try
            {
                // FILTRADO POR SEMILLERO: Asegura que el líder solo gestione sus propios recursos.
                ViewBag.SemillerosDisponibles = db.semillero.Where(s => s.ID_semillero == idSemilleroLider).ToList();

                var misProyectos = db.Proyectos.Where(p => p.ID_semillero == idSemilleroLider).ToList();
                ViewBag.ListaProyectos = misProyectos;
                ViewBag.ProyectosDisponibles = misProyectos;

                // Recupera solo las fases que pertenecen a proyectos del semillero del líder.
                ViewBag.FasesDisponibles = (from f in db.FasesProyecto
                                            join p in db.Proyectos on f.ID_proyecto equals p.ID_proyecto
                                            where p.ID_semillero == idSemilleroLider
                                            select f).ToList() ?? new List<FaseProyecto>();

                // CÁLCULO DE SIGUIENTES IDs: Obtiene el próximo ID disponible para nuevas inserciones.
                var ultProyecto = db.Proyectos.OrderByDescending(p => p.ID_proyecto).FirstOrDefault();
                ViewBag.NextProyectoID = (ultProyecto != null) ? ultProyecto.ID_proyecto + 1 : 101;

                var ultFase = db.FasesProyecto.OrderByDescending(f => f.ID_fase_proyecto).FirstOrDefault();
                ViewBag.NextFaseID = (ultFase != null) ? ultFase.ID_fase_proyecto + 1 : 501;

                var ultAct = db.ActividadesProyecto.OrderByDescending(a => a.ID_activida_proyecto).FirstOrDefault();
                ViewBag.NextActividadID = (ultAct != null) ? ultAct.ID_activida_proyecto + 1 : 901;

                // MAPEO DTO (Usando ExpandoObject): Consolida datos de Fases y Proyectos para la vista.
                ViewBag.ListaFases = (from f in db.FasesProyecto
                                      join p in db.Proyectos on f.ID_proyecto equals p.ID_proyecto
                                      where p.ID_semillero == idSemilleroLider
                                      select new { f.ID_fase_proyecto, f.nombre_fase_proyecto, f.descripcion_fase_proyecto, p.nombre_proyecto })
                                      .ToList().Select(x => {
                                          dynamic exp = new ExpandoObject();
                                          exp.ID_fase_proyecto = x.ID_fase_proyecto;
                                          exp.nombre_fase_proyecto = x.nombre_fase_proyecto;
                                          exp.descripcion_fase_proyecto = x.descripcion_fase_proyecto;
                                          exp.nombre_proyecto = x.nombre_proyecto;
                                          return exp;
                                      }).ToList();

                // Mapeo DTO: Consolida Actividades con su Fase padre.
                ViewBag.ListaActividades = (from a in db.ActividadesProyecto
                                            join f in db.FasesProyecto on a.ID_fase_proyecto equals f.ID_fase_proyecto
                                            join p in db.Proyectos on f.ID_proyecto equals p.ID_proyecto
                                            where p.ID_semillero == idSemilleroLider
                                            select new { a.ID_activida_proyecto, a.nombre_actividad_proyecto, a.descripcion_actividad_proyecto, a.fecha_inicio_actividad_proyecto, a.fecha_fin_actividad_proyecto, f.nombre_fase_proyecto })
                                            .ToList().Select(x => {
                                                dynamic exp = new ExpandoObject();

                                                // ----------------------------------------------------
                                                // CORRECCIONES APLICADAS AQUÍ ABAJO
                                                // ----------------------------------------------------
                                                exp.ID_activida_proyecto = x.ID_activida_proyecto;
                                                exp.nombre_actividad_proyecto = x.nombre_actividad_proyecto;
                                                exp.descripcion_actividad_proyecto = x.descripcion_actividad_proyecto;
                                                exp.fecha_inicio_actividad_proyecto = x.fecha_inicio_actividad_proyecto;
                                                exp.fecha_fin_actividad_proyecto = x.fecha_fin_actividad_proyecto;
                                                exp.nombre_fase_proyecto = x.nombre_fase_proyecto;
                                                // ----------------------------------------------------

                                                return exp;
                                            }).ToList();
            }
            catch (Exception)
            {
                // Bloque de seguridad: En caso de error, inicializa listas vacías para evitar errores de referencia nula en la vista.
                ViewBag.SemillerosDisponibles = new List<GestionSemillero1.Models.semillero>();
                ViewBag.ListaProyectos = new List<GestionSemillero1.Models.Proyecto>();
                ViewBag.ProyectosDisponibles = new List<GestionSemillero1.Models.Proyecto>();
                ViewBag.FasesDisponibles = new List<GestionSemillero1.Models.FaseProyecto>();
                ViewBag.ListaFases = new List<dynamic>();
                ViewBag.ListaActividades = new List<dynamic>();
            }
        }

        // =========================================================================
        // MÓDULO DE CONSULTAR EVENTOS
        // =========================================================================
        public ActionResult ConsultarEventos(string criterioFiltro, string valorBusqueda)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            if (Session["UsuarioLogueado"] == null || Session["TipoUsuario"]?.ToString().ToLower() != "lider")
            {
                return RedirectToAction("Login", "Account");
            }

            var query = from e in db.Eventos
                        join s in db.semillero on e.ID_semillero equals s.ID_semillero
                        select new
                        {
                            e.ID_evento,
                            e.nombre_evento,
                            e.descripción_evento,
                            e.fecha_evento,
                            s.nombre_semillero
                        };

            if (!string.IsNullOrEmpty(criterioFiltro) && !string.IsNullOrEmpty(valorBusqueda))
            {
                string busqueda = valorBusqueda.Trim().ToLower();

                if (criterioFiltro == "nombre")
                {
                    query = query.Where(e => e.nombre_evento.ToLower().Contains(busqueda));
                }
                else if (criterioFiltro == "semillero")
                {
                    query = query.Where(s => s.nombre_semillero.ToLower().Contains(busqueda));
                }
                else if (criterioFiltro == "fecha")
                {
                    if (DateTime.TryParse(valorBusqueda, out DateTime fechaFiltro))
                    {
                        query = query.Where(e => e.fecha_evento == fechaFiltro);
                    }
                }
            }

            ViewBag.EventosResultado = query.ToList().Select(x =>
            {
                dynamic expando = new ExpandoObject();
                expando.ID_evento = x.ID_evento;
                expando.nombre_evento = x.nombre_evento;
                expando.descripción_evento = x.descripción_evento;
                expando.fecha_evento = x.fecha_evento;
                expando.nombre_semillero = x.nombre_semillero;
                return expando;
            }).ToList();

            ViewBag.CriterioActual = criterioFiltro;
            ViewBag.ValorActual = valorBusqueda;

            return View();
        }

        // =========================================================================
        // 🖨️ MÓDULO DE REPORTES EXCLUSIVOS DEL LÍDER
        // =========================================================================
        public ActionResult Reportes()
        {
            if (Session["IDUsuario"] == null) return RedirectToAction("Login", "Account");
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

                using (var db = new DbSemillero())
                {
                    db.Configuration.ProxyCreationEnabled = false;
                    db.Configuration.LazyLoadingEnabled = false;

                    // Aquí tienes la lista de los reportes
                    switch (nombreReporte)
                    {
                        // --- ADMIN ---
                        case "Reporte_Investigadores_General":
                            reportDocument.SetDataSource(db.investigadores.ToList());
                            break;
                        case "Reporte_Lideres_General":
                            var idsLideres = db.Usuarios.Where(u => u.tipo_usuario.Contains("Lider") || u.tipo_usuario.Contains("Administrador")).Select(u => u.ID_usuario).ToList();
                            reportDocument.SetDataSource(db.investigadores.Where(i => idsLideres.Contains(i.ID_usuario)).ToList());
                            break;
                        case "Reporte_Semilleros_Lineas":
                            reportDocument.SetDataSource(db.semillero.ToList());
                            break;
                        case "Reporte_Proyectos_Eventos":
                            reportDocument.SetDataSource(db.Proyectos.ToList());
                            break;
                        case "Reporte_Eventos_General":
                            reportDocument.SetDataSource(db.Eventos.ToList());
                            break;

                        // --- LÍDER ---
                        case "Reporte_Mis_Investigadores":
                            reportDocument.SetDataSource(db.investigadores.ToList());
                            break;
                        case "Reporte_Mis_Proyectos":
                            reportDocument.SetDataSource(db.Proyectos.ToList());
                            break;
                        case "Reporte_Avance_Fases":
                            reportDocument.SetDataSource(db.FasesProyecto.ToList());
                            break;
                        case "Reporte_Reuniones_Por_Semillero":
                            reportDocument.SetDataSource(db.Reunion.ToList());
                            break;
                        case "Reporte_Mis_Eventos":
                            reportDocument.SetDataSource(db.Eventos.ToList());
                            break;
                    }
                }

                // LIMPIEZA DE CONEXIÓN
               // foreach (CrystalDecisions.CrystalReports.Engine.Table table in reportDocument.Database.Tables)
                {
                //    var logOnInfo = table.LogOnInfo;
                //    logOnInfo.ConnectionInfo.IntegratedSecurity = true;
                //    table.ApplyLogOnInfo(logOnInfo);
                }

                //  foreach (ReportDocument subReport in reportDocument.Subreports)
                {
                    // foreach (CrystalDecisions.CrystalReports.Engine.Table table in subReport.Database.Tables)
                    {
                        //       var logOnInfo = table.LogOnInfo;
                        //       logOnInfo.ConnectionInfo.IntegratedSecurity = true;
                        //       table.ApplyLogOnInfo(logOnInfo);
                    }
                }

                // 🌟 LA MAGIA ESTÁ AQUÍ: Convertimos a arreglo de bytes para independizarlo del reporte
                // Magia para exportar a bytes sin tocar discos ni conexiones largas
                Stream stream = reportDocument.ExportToStream(ExportFormatType.PortableDocFormat);
                byte[] byteArray;
                using (MemoryStream ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    byteArray = ms.ToArray();
                }

                // Limpieza INMEDIATA antes de retornar el archivo
                reportDocument.Close();
                reportDocument.Dispose();

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

}
