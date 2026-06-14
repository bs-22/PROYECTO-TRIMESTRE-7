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
        // 📅 VISTA PRINCIPAL: GESTIONAR REUNIONES
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

        // 🌟 NUEVO MÉTODO API: Devuelve los IDs de los usuarios que asisten a una reunión específica
        [HttpGet]
        public JsonResult ObtenerAsistentesReunion(decimal idReunion)
        {
            var asistentesIds = db.AsistenciaReunion
                                  .Where(a => a.ID_reunion == idReunion)
                                  .Select(a => a.ID_usuario.ToString())
                                  .ToList();

            return Json(asistentesIds, JsonRequestBehavior.AllowGet);
        }

        // =========================================================================
        // 🔄 ACCIÓN UNIFICADA: CREAR O ACTUALIZAR REUNIÓN
        // =========================================================================
        [HttpPost]
        public ActionResult ProcesarReunion(FormCollection form, decimal[] investigadoresSeleccionados)
        {
            if (Session["IDUsuario"] == null) return RedirectToAction("Login", "Account");
            decimal idLider = Convert.ToDecimal(Session["IDUsuario"]);

            // Captura de datos del formulario de manera segura
            string idRaw = form["ID_reunion"];
            string descripcion_reunion = form["descripcion_reunion"];
            string hora_reunion = form["hora_reunion"];
            string hora_fin_reunion = form["hora_fin_reunion"];
            string lugar_reunion = form["lugar_reunion"];
            string fecha_reunion = form["fecha_reunion"];

            DateTime fecha = DateTime.Parse(fecha_reunion);
            TimeSpan hIni = TimeSpan.Parse(hora_reunion);
            TimeSpan hFin = TimeSpan.Parse(hora_fin_reunion);

            // 1. REGLA DE NEGOCIO: Validar Rango permitido obligatoriamente de 6 AM a 6 PM
            TimeSpan limiteApertura = new TimeSpan(6, 0, 0);
            TimeSpan limiteCierre = new TimeSpan(18, 0, 0);

            if (hIni < limiteApertura || hIni > limiteCierre || hFin < limiteApertura || hFin > limiteCierre)
            {
                TempData["Error"] = "Operación Rechazada: El rango de horario permitido para comités es estrictamente de 06:00 AM a 06:00 PM.";
                return RedirectToAction("Index");
            }

            using (var transaccion = db.Database.BeginTransaction())
            {
                try
                {
                    if (string.IsNullOrEmpty(idRaw))
                    {
                        // =========================================================
                        // MODO: AGENDAR NUEVA REUNIÓN
                        // =========================================================

                        // 2. REGLA DE NEGOCIO: Validar disponibilidad de agenda de los participantes seleccionados
                        if (investigadoresSeleccionados != null)
                        {
                            foreach (var idInv in investigadoresSeleccionados)
                            {
                                var inv = db.investigadores.FirstOrDefault(i => i.ID_investigador == idInv);
                                if (inv != null)
                                {
                                    // Traemos las reuniones que el usuario ya tenga agendadas ese mismo día a memoria para comparar limpiamente
                                    var reunionesAsignadas = (from a in db.AsistenciaReunion
                                                              join r in db.Reunion on a.ID_reunion equals r.ID_reunion
                                                              where a.ID_usuario == inv.ID_usuario && r.fecha_reunion == fecha
                                                              select r).ToList();

                                    foreach (var r in reunionesAsignadas)
                                    {
                                        TimeSpan dbIni = TimeSpan.Parse(r.hora_reunion.ToString());
                                        TimeSpan dbFin = TimeSpan.Parse(r.hora_fin_reunion.ToString());

                                        // Verificamos si existe traslape de tiempos
                                        if ((hIni >= dbIni && hIni < dbFin) || (hFin > dbIni && hFin <= dbFin) || (hIni <= dbIni && hFin >= dbFin))
                                        {
                                            throw new Exception($"El participante {inv.nombre_investigador} {inv.apellido_investigador} no está disponible. Ya se encuentra convocado a la reunión institucional '{r.descripcion_reunion}' en el horario de {dbIni.ToString(@"hh\:mm")} a {dbFin.ToString(@"hh\:mm")}.");
                                        }
                                    }
                                }
                            }
                        }

                        // Cálculo incremental del consecutivo de la llave primaria
                        decimal siguienteId = 5001;
                        var maxId = db.Database.SqlQuery<decimal?>("SELECT MAX(ID_reunion) FROM reunion").FirstOrDefault();
                        if (maxId.HasValue) siguienteId = maxId.Value + 1;

                        // Inserción física de la reunión usando SQL Nativo
                        string sqlInsert = "INSERT INTO reunion (ID_reunion, descripcion_reunion, hora_reunion, hora_fin_reunion, lugar_reunion, fecha_reunion, ID_semillero, estado_reunion) " +
                                           "VALUES (@id, @desc, @hini, @hfin, @lug, @fec, 2001, 'Programada')";

                        db.Database.ExecuteSqlCommand(sqlInsert,
                            new System.Data.SqlClient.SqlParameter("@id", siguienteId),
                            new System.Data.SqlClient.SqlParameter("@desc", descripcion_reunion),
                            new System.Data.SqlClient.SqlParameter("@hini", hora_reunion),
                            new System.Data.SqlClient.SqlParameter("@hfin", hora_fin_reunion),
                            new System.Data.SqlClient.SqlParameter("@lug", lugar_reunion),
                            new System.Data.SqlClient.SqlParameter("@fec", fecha));

                        // El Líder se auto-asigna al acta de forma automática
                        InsertarAsistencia(siguienteId, idLider);

                        // Registro estructurado de los investigadores vinculados
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

                        TempData["Success"] = "La reunión institucional ha sido agendada e integrada al calendario correctamente.";
                    }
                    else
                    {
                        // =========================================================
                        // MODO: ACTUALIZAR REUNIÓN EXISTENTE
                        // =========================================================
                        decimal idModificar = Convert.ToDecimal(idRaw);
                        var reunionExistente = db.Reunion.FirstOrDefault(r => r.ID_reunion == idModificar);

                        if (reunionExistente == null) throw new Exception("El código de reunión no coincide con los registros.");

                        // 3. REGLA DE NEGOCIO: Validar políticas de anticipación horaria y de fechas
                        if (reunionExistente.fecha_reunion != fecha)
                        {
                            // Si altera la fecha, requiere mínimo 1 día (24 horas) completo de anticipación
                            if ((reunionExistente.fecha_reunion - DateTime.Now.Date).TotalDays < 1)
                            {
                                throw new Exception("Seguridad de Agenda: Las modificaciones de fecha requieren un mínimo de 24 horas de anticipación.");
                            }
                        }

                        TimeSpan dbHoraIni = TimeSpan.Parse(reunionExistente.hora_reunion.ToString());
                        if (dbHoraIni != hIni)
                        {
                            // Si altera la hora, requiere mínimo 1 hora exacta de anticipación
                            DateTime momentoReunionOriginal = reunionExistente.fecha_reunion.Add(dbHoraIni);
                            if ((momentoReunionOriginal - DateTime.Now).TotalHours < 1)
                            {
                                throw new Exception("Seguridad de Agenda: Los cambios de horario sobre reuniones programadas exigen mínimo 1 hora de anticipación.");
                            }
                        }

                        // Ejecutamos la actualización física
                        string sqlUpdate = "UPDATE reunion SET descripcion_reunion=@desc, hora_reunion=@hini, hora_fin_reunion=@hfin, lugar_reunion=@lug, fecha_reunion=@fec WHERE ID_reunion=@id";
                        db.Database.ExecuteSqlCommand(sqlUpdate,
                            new System.Data.SqlClient.SqlParameter("@desc", descripcion_reunion),
                            new System.Data.SqlClient.SqlParameter("@hini", hora_reunion),
                            new System.Data.SqlClient.SqlParameter("@hfin", hora_fin_reunion),
                            new System.Data.SqlClient.SqlParameter("@lug", lugar_reunion),
                            new System.Data.SqlClient.SqlParameter("@fec", fecha),
                            new System.Data.SqlClient.SqlParameter("@id", idModificar));

                        // Actualización de participantes por reemplazo limpio de cascada manual
                        string sqlClearAsistencias = "DELETE FROM asistencia_reunion WHERE ID_reunion=@id AND ID_usuario != @idLider";
                        db.Database.ExecuteSqlCommand(sqlClearAsistencias,
                            new System.Data.SqlClient.SqlParameter("@id", idModificar),
                            new System.Data.SqlClient.SqlParameter("@idLider", idLider));

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

                        TempData["Success"] = "Los detalles de la reunión han sido actualizados con éxito.";
                    }

                    transaccion.Commit();
                }
                catch (Exception ex)
                {
                    transaccion.Rollback();
                    TempData["Error"] = ex.Message;
                }
            }

            return RedirectToAction("Index");
        }

        // =========================================================================
        // 🗑️ ACCIÓN: ELIMINAR REUNIÓN FÍSICAMENTE
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
                    // 1. Limpiamos las dependencias asociativas de la tabla puente (Evita violaciones de integridad referencial)
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
        // GET: Lider/ConsultarReuniones
        public ActionResult ConsultarReuniones(string criterio, string valor)
        {
            // Validación de seguridad de la sesión
            if (Session["IDUsuario"] == null) return RedirectToAction("Login", "Account");

            decimal idUsuarioActual = Convert.ToDecimal(Session["IDUsuario"]);

            // 1. Encontrar el ID_semillero al que pertenece/lidera el usuario actual
            var idSemilleroLider = db.investigadores
                                    .Where(i => i.ID_usuario == idUsuarioActual)
                                    .Select(i => i.ID_semillero)
                                    .FirstOrDefault();

            // VALIDACIÓN DE SEGURIDAD: Si el usuario no tiene semillero asignado en investigadores, protegemos la vista
            if (idSemilleroLider == 0)
            {
                ViewBag.TopIDs = new List<string>();
                ViewBag.TopFechas = new List<string>();
                return View(new List<Reunion>());
            }

            // 🌟 REGLA MAESTRA INTEGRADA PERFECTAMENTE: 
            // Trae las reuniones de su propio semillero OR donde el líder aparezca en la lista de asistencia
            var queryBase = (from r in db.Reunion
                             where r.ID_semillero == idSemilleroLider ||
                                   db.AsistenciaReunion.Any(a => a.ID_reunion == r.ID_reunion && a.ID_usuario == idUsuarioActual)
                             select r).Distinct();

            // 2. Cargar las 5 sugerencias del autocompletado basadas en este nuevo universo autorizado
            ViewBag.TopIDs = queryBase.Select(r => r.ID_reunion.ToString())
                                       .Distinct()
                                       .Take(5)
                                       .ToList();

            var fechasRaw = queryBase.Select(r => r.fecha_reunion)
                                     .Distinct()
                                     .Take(5)
                                     .ToList();

            // 🌟 CORRECCIÓN AQUÍ: Se añade .AsEnumerable() para que el formato de fecha se procese en memoria y no rompa SQL
            ViewBag.TopFechas = fechasRaw
                                .AsEnumerable()
                                .Select(f => f.ToString("dd/MM/yyyy"))
                                .Where(x => !string.IsNullOrEmpty(x))
                                .ToList();

            // 3. Aplicar filtros de búsqueda por ID o Fecha elegidos por el usuario
            var reuniones = queryBase.AsQueryable();

            if (!string.IsNullOrWhiteSpace(valor))
            {
                if (criterio == "ID" && decimal.TryParse(valor, out decimal id))
                {
                    reuniones = reuniones.Where(r => r.ID_reunion == id);
                }
                else if (criterio == "Fecha")
                {
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
        private void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }




        // GET: Lider/GestionarSemillero
        public ActionResult GestionarSemillero(string semilleroFiltro, string lineaFiltro, string fechaFiltro, string seccionCargada, decimal? idSemilleroSeccion4)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            if (Session["UsuarioLogueado"] == null || Session["TipoUsuario"]?.ToString().ToLower() != "lider")
            {
                return RedirectToAction("Login", "Account");
            }

            decimal siguienteId = 2001;
            var ultimoSemillero = db.semillero.OrderByDescending(s => s.ID_semillero).FirstOrDefault();
            if (ultimoSemillero != null)
            {
                siguienteId = ultimoSemillero.ID_semillero + 1;
            }
            ViewBag.SiguienteID = siguienteId;

            var todosLosSemilleros = db.semillero.ToList() ?? new List<semillero>();
            ViewBag.SemillerosFiltro = todosLosSemilleros;
            ViewBag.LineasFiltro = todosLosSemilleros.Select(s => s.linea_investigacion).Distinct().ToList();

            ViewBag.SeccionActual = string.IsNullOrEmpty(seccionCargada) ? "semilleros" : seccionCargada;

            if (idSemilleroSeccion4.HasValue)
            {
                ViewBag.SeccionActual = "investigadores";
                ViewBag.IdSemilleroSeleccionado4 = idSemilleroSeccion4.Value;

                var semActual = db.semillero.Find(idSemilleroSeccion4.Value);
                ViewBag.NombreSemilleroSeleccionado4 = semActual != null ? semActual.nombre_semillero : "";

                List<InvestigadorFiltroViewModel> listaDeMiembros = (from i in db.investigadores
                                                                     join u in db.Usuarios on i.ID_usuario equals u.ID_usuario
                                                                     where i.ID_semillero == idSemilleroSeccion4.Value
                                                                     select new InvestigadorFiltroViewModel
                                                                     {
                                                                         ID_investigador = i.ID_investigador,
                                                                         NombreCompleto = i.nombre_investigador + " " + i.apellido_investigador,
                                                                         correo_usuario = u.correo_usuario,
                                                                         estado_usuario = u.estado_usuario,
                                                                         ID_usuario = i.ID_usuario
                                                                     }).ToList();

                ViewBag.InvestigadoresDeEsteSemillero = listaDeMiembros;
            }

            if (ViewBag.SeccionActual == "filtros")
            {
                var query = db.semillero.AsQueryable();

                if (!string.IsNullOrEmpty(semilleroFiltro))
                {
                    decimal idSem = Convert.ToDecimal(semilleroFiltro);
                    query = query.Where(s => s.ID_semillero == idSem);
                }
                if (!string.IsNullOrEmpty(lineaFiltro))
                {
                    query = query.Where(s => s.linea_investigacion == lineaFiltro);
                }
                if (!string.IsNullOrEmpty(fechaFiltro))
                {
                    DateTime fecha = Convert.ToDateTime(fechaFiltro);
                    query = query.Where(s => s.fecha_creacion_semillero == fecha);
                }

                ViewBag.ResultadoFiltros = query.ToList();
            }

            return View(todosLosSemilleros);
        }

        // POST: Lider/GestionarSemillero
        [HttpPost]
        public ActionResult GestionarSemillero(semillero datosSemillero, string accion)
        {
            if (Session["UsuarioLogueado"] == null) return RedirectToAction("Login", "Account");

            try
            {
                if (accion == "agregar")
                {
                    datosSemillero.estado = "activo";
                    db.semillero.Add(datosSemillero);
                    db.SaveChanges();
                }
                else if (accion == "actualizar")
                {
                    var registro = db.semillero.Find(datosSemillero.ID_semillero);
                    if (registro != null)
                    {
                        registro.nombre_semillero = datosSemillero.nombre_semillero;
                        registro.linea_investigacion = datosSemillero.linea_investigacion;
                        registro.fecha_creacion_semillero = datosSemillero.fecha_creacion_semillero;
                        registro.descripcion_semillero = datosSemillero.descripcion_semillero;
                        db.SaveChanges();
                    }
                }
                else if (accion == "eliminar")
                {
                    var registro = db.semillero.Find(datosSemillero.ID_semillero);
                    if (registro != null)
                    {
                        db.semillero.Remove(registro);
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
            }

            return RedirectToAction("GestionarSemillero", new { seccionCargada = "semilleros" });
        }

        // POST: Lider/ProcesarAccionesInvestigadores
        [HttpPost]
        public ActionResult ProcesarAccionesInvestigadores(decimal? idSemilleroSeleccionado, string idUsuarioSeleccionado, string accionInvestigador, string nuevoInvestigadorNombre, string nuevoInvestigadorApellido, string nuevoInvestigadorDoc, int? nuevoInvestigadorEdad, decimal? nuevoInvestigadorTel, string nuevoInvestigadorCorreo, string nuevoInvestigadorContrasena)
        {
            if (Session["UsuarioLogueado"] == null) return RedirectToAction("Login", "Account");

            if (accionInvestigador == "agregar")
            {
                using (var transaccion = db.Database.BeginTransaction())
                {
                    try
                    {
                        var correoLimpio = nuevoInvestigadorCorreo.Trim().ToLower();

                        bool existeUsuario = db.Usuarios.Any(u => u.correo_usuario.Trim().ToLower() == correoLimpio);
                        if (existeUsuario)
                        {
                            throw new Exception("El correo ingresado ya se encuentra registrado en el sistema.");
                        }

                        Usuario nuevoUsuario = new Usuario();
                        decimal nextUserId = 1;
                        var ultimoUser = db.Usuarios.OrderByDescending(u => u.ID_usuario).FirstOrDefault();
                        if (ultimoUser != null) nextUserId = ultimoUser.ID_usuario + 1;

                        nuevoUsuario.ID_usuario = nextUserId;
                        nuevoUsuario.correo_usuario = nuevoInvestigadorCorreo.Trim();
                        nuevoUsuario.contraseña_usuario = nuevoInvestigadorContrasena;
                        nuevoUsuario.tipo_usuario = "investigador";
                        nuevoUsuario.estado_usuario = "activo";

                        db.Usuarios.Add(nuevoUsuario);
                        db.SaveChanges();

                        investigadores nuevoInv = new investigadores();
                        decimal nextInvId = 1;
                        var ultimoInv = db.investigadores.OrderByDescending(i => i.ID_investigador).FirstOrDefault();
                        if (ultimoInv != null) nextInvId = ultimoInv.ID_investigador + 1;

                        nuevoInv.ID_investigador = nextInvId;
                        nuevoInv.nombre_investigador = nuevoInvestigadorNombre;
                        nuevoInv.apellido_investigador = nuevoInvestigadorApellido;
                        nuevoInv.tipo_documento = nuevoInvestigadorDoc;
                        nuevoInv.edad_investigador = nuevoInvestigadorEdad ?? 20;
                        nuevoInv.telefono_investigador = nuevoInvestigadorTel ?? 0;
                        nuevoInv.ID_usuario = nuevoUsuario.ID_usuario;
                        nuevoInv.ID_semillero = idSemilleroSeleccionado.Value;

                        db.investigadores.Add(nuevoInv);
                        db.SaveChanges();

                        transaccion.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaccion.Rollback();
                        TempData["Error"] = ex.Message;
                    }
                }
            }
            else
            {
                try
                {
                    if (!string.IsNullOrEmpty(idUsuarioSeleccionado))
                    {
                        decimal idUser = Convert.ToDecimal(idUsuarioSeleccionado);
                        var usuario = db.Usuarios.Find(idUser);

                        if (usuario != null)
                        {
                            if (accionInvestigador == "habilitar")
                            {
                                usuario.estado_usuario = "activo";
                                db.SaveChanges();
                            }
                            else if (accionInvestigador == "deshabilitar")
                            {
                                usuario.estado_usuario = "inactivo";
                                db.SaveChanges();
                            }
                            else if (accionInvestigador == "eliminar" && idSemilleroSeleccionado.HasValue)
                            {
                                var relacion = db.investigadores.FirstOrDefault(i => i.ID_usuario == idUser && i.ID_semillero == idSemilleroSeleccionado.Value);
                                if (relacion != null)
                                {
                                    db.investigadores.Remove(relacion);
                                    db.SaveChanges();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    TempData["Error"] = ex.Message;
                }
            }

            return RedirectToAction("GestionarSemillero", new { seccionCargada = "investigadores", idSemilleroSeccion4 = idSemilleroSeleccionado });
        }

        // =========================================================================
        // MÉTODOS AUXILIARES: REGISTRAR PROYECTO
        // =========================================================================
        private void CargarDatosVista(string pestañaActiva)
        {
            ViewBag.PestañaCargada = string.IsNullOrEmpty(pestañaActiva) ? "proyectos" : pestañaActiva;

            try
            {
                ViewBag.SemillerosDisponibles = db.semillero.ToList() ?? new List<semillero>();
                ViewBag.ProyectosDisponibles = db.Proyectos.ToList() ?? new List<Proyecto>();
                ViewBag.FasesDisponibles = db.FasesProyecto.ToList() ?? new List<FaseProyecto>();
                ViewBag.ListaProyectos = db.Proyectos.ToList() ?? new List<Proyecto>();

                decimal nextProyectoId = 101;
                var ultProyecto = db.Proyectos.OrderByDescending(p => p.ID_proyecto).FirstOrDefault();
                if (ultProyecto != null) nextProyectoId = ultProyecto.ID_proyecto + 1;
                ViewBag.NextProyectoID = nextProyectoId;

                decimal nextFaseId = 501;
                var ultFase = db.FasesProyecto.OrderByDescending(f => f.ID_fase_proyecto).FirstOrDefault();
                if (ultFase != null) nextFaseId = ultFase.ID_fase_proyecto + 1;
                ViewBag.NextFaseID = nextFaseId;

                decimal nextActividadId = 901;
                var ultAct = db.ActividadesProyecto.OrderByDescending(a => a.ID_activida_proyecto).FirstOrDefault();
                if (ultAct != null) nextActividadId = ultAct.ID_activida_proyecto + 1;
                ViewBag.NextActividadID = nextActividadId;

                var queryFases = (from f in db.FasesProyecto
                                  join p in db.Proyectos on f.ID_proyecto equals p.ID_proyecto into joined
                                  from p in joined.DefaultIfEmpty()
                                  select new { f.ID_fase_proyecto, f.nombre_fase_proyecto, f.descripcion_fase_proyecto, NombreProyecto = p != null ? p.nombre_proyecto : "Sin proyecto" }).ToList();

                ViewBag.ListaFases = queryFases.Select(x =>
                {
                    dynamic expando = new ExpandoObject();
                    expando.ID_fase_proyecto = x.ID_fase_proyecto;
                    expando.nombre_fase_proyecto = x.nombre_fase_proyecto;
                    expando.descripcion_fase_proyecto = x.descripcion_fase_proyecto;
                    expando.nombre_proyecto = x.NombreProyecto;
                    return expando;
                }).ToList();

                var queryActividades = (from a in db.ActividadesProyecto
                                        join f in db.FasesProyecto on a.ID_fase_proyecto equals f.ID_fase_proyecto into joined
                                        from f in joined.DefaultIfEmpty()
                                        select new { a.ID_activida_proyecto, a.nombre_actividad_proyecto, a.descripcion_actividad_proyecto, a.fecha_inicio_actividad_proyecto, a.fecha_fin_actividad_proyecto, NombreFase = f != null ? f.nombre_fase_proyecto : "Sin fase" }).ToList();

                ViewBag.ListaActividades = queryActividades.Select(x =>
                {
                    dynamic expando = new ExpandoObject();
                    expando.ID_activida_proyecto = x.ID_activida_proyecto;
                    expando.nombre_actividad_proyecto = x.nombre_actividad_proyecto;
                    expando.descripcion_actividad_proyecto = x.descripcion_actividad_proyecto;
                    expando.fecha_inicio_actividad_proyecto = x.fecha_inicio_actividad_proyecto;
                    expando.fecha_fin_actividad_proyecto = x.fecha_fin_actividad_proyecto;
                    expando.nombre_fase_proyecto = x.NombreFase;
                    return expando;
                }).ToList();
            }
            catch (Exception)
            {
                if (ViewBag.SemillerosDisponibles == null) ViewBag.SemillerosDisponibles = new List<semillero>();
                if (ViewBag.ProyectosDisponibles == null) ViewBag.ProyectosDisponibles = new List<Proyecto>();
                if (ViewBag.FasesDisponibles == null) ViewBag.FasesDisponibles = new List<FaseProyecto>();
                if (ViewBag.ListaProyectos == null) ViewBag.ListaProyectos = new List<Proyecto>();
                if (ViewBag.ListaFases == null) ViewBag.ListaFases = new List<object>();
                if (ViewBag.ListaActividades == null) ViewBag.ListaActividades = new List<object>();
            }
        }

        // GET: Lider/RegistrarProyecto
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

        // POST: Lider/RegistrarProyecto
        [HttpPost]
        public ActionResult RegistrarProyecto(Proyecto p, FaseProyecto f, ActividadProyecto a, string tipoSubmit, string accionCrud)
        {
            if (Session["UsuarioLogueado"] == null) return RedirectToAction("Login", "Account");

            string pestañaDestino = string.IsNullOrEmpty(tipoSubmit) ? "proyectos" : tipoSubmit;

            try
            {
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

            CargarDatosVista(pestañaDestino);
            return View();
        }

        // =========================================================================
        // MÉTODOS DEL MÓDULO: CONSULTAR EVENTOS
        // =========================================================================
        // GET: Lider/ConsultarEventos
        public ActionResult ConsultarEventos(string criterioFiltro, string valorBusqueda)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            if (Session["UsuarioLogueado"] == null || Session["TipoUsuario"]?.ToString().ToLower() != "lider")
            {
                return RedirectToAction("Login", "Account");
            }

            // Consulta corregida uniendo Eventos con Semilleros de forma segura
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

            // Filtrado condicional según la selección en el combo
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

            // Aplanamiento dinámico definitivo para evitar errores de Binder en la vista Razor
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


        //REPORTE 
        // 1. GET: Administrador/Reportes
        public ActionResult Reportes()
        {
            using (var db = new DbSemillero()) // REEMPLAZA por tu DbContext real
            {
                // Métricas rápidas para las tarjetas informativas de la vista
                ViewBag.TotalSemilleros = db.semillero.Count();
                ViewBag.TotalUsuarios = db.Usuarios.Count(); // Ajusta según tu tabla de usuarios
            }
            return View();
        }

        // 2. POST: Administrador/GenerarCrystalReport
        [HttpPost]
        public ActionResult GenerarCrystalReport(string nombreReporte)
        {
            try
            {
                ReportDocument reportDocument = new ReportDocument();

                // Mapeo de la ruta física de tus plantillas .rpt en el servidor
                string rutaReporte = Path.Combine(Server.MapPath("~/Reports"), nombreReporte + ".rpt");

                if (!System.IO.File.Exists(rutaReporte))
                {
                    TempData["Error"] = "El archivo físico " + nombreReporte + ".rpt no se encuentra en la carpeta /Reports.";
                    return RedirectToAction("Reportes");
                }

                reportDocument.Load(rutaReporte);

                using (var db = new DbSemillero()) // REEMPLAZA por tu DbContext real
                {
                    // Lógica de datos parametrizada según lo solicitado
                    switch (nombreReporte)
                    {
                        case "Reporte_Investigadores_General":
                            // 1. Reporte de todos los investigadores de todos los semilleros
                            var investigadores = db.Usuarios.Where(u => u.tipo_usuario == "Investigador").ToList();
                            reportDocument.SetDataSource(investigadores);
                            break;

                        case "Reporte_Lideres_General":
                            // 2. Reporte de todos los líderes instructores
                            var lideres = db.Usuarios.Where(u => u.tipo_usuario == "Lider").ToList();
                            reportDocument.SetDataSource(lideres);
                            break;

                        case "Reporte_Semilleros_Lineas":
                            // 3. Conteo de semilleros totales y sus líneas de investigación
                            var semilleros = db.semillero.ToList();
                            reportDocument.SetDataSource(semilleros);
                            break;

                        case "Reporte_Proyectos_Eventos":
                            // 4. Reporte combinado de proyectos y eventos registrados en el sistema
                            var proyectos = db.Database.SqlQuery<Proyecto>("SELECT * FROM proyecto").ToList();
                            reportDocument.SetDataSource(proyectos);
                            break;

                        case "Reporte_Reuniones_Por_Semillero":
                            // 5. Reporte de cuántas reuniones tiene agendadas cada semillero
                            var reuniones = db.Database.SqlQuery<Reunion>("SELECT * FROM Reunion").ToList();
                            reportDocument.SetDataSource(reuniones);
                            break;
                    }
                }

                // Compilación y conversión nativa del reporte de Crystal Reports a PDF
                Stream stream = reportDocument.ExportToStream(ExportFormatType.PortableDocFormat);
                stream.Seek(0, SeekOrigin.Begin);

                string nombreArchivoDescarga = nombreReporte + "_" + DateTime.Now.ToString("yyyyMMdd") + ".pdf";

                // Retorna el archivo PDF directo para descarga o visualización limpia
                return File(stream, "application/pdf", nombreArchivoDescarga);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al compilar en el motor de Crystal Reports: " + ex.Message;
                return RedirectToAction("Reportes");
            }
        }

    }

}
