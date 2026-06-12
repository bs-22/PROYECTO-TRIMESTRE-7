using System.Data.Entity;
using System.Drawing;

namespace GestionSemillero1.Models
{
    public class DbSemillero : DbContext
    {
        public DbSemillero() : base("name=DbSemillero")
        {
        }

        // Definición correcta de la tabla Reunion
        public DbSet<Reunion> Reunion { get; set; }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<semillero> semillero { get; set; }
        public DbSet<investigadores> investigadores { get; set; }
        public DbSet<Proyecto> Proyectos { get; set; }
        public DbSet<FaseProyecto> FasesProyecto { get; set; }
        public DbSet<ActividadProyecto> ActividadesProyecto { get; set; }
        public DbSet<Evento> Eventos { get; set; }

        public virtual DbSet<AsistenciaReunion> AsistenciaReunion { get; set; }
    }
}