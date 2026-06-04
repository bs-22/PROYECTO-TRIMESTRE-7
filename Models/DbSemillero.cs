using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace GestionSemillero1.Models
{
    public class DbSemillero : DbContext
    {
        public DbSemillero() : base("name=DbSemillero")
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
    }
}