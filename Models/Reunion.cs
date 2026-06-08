using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionSemillero1.Models
{
    [Table("reunion")]
    public class Reunion
    {
        [Key]
        public int ID_reunion { get; set; }

        public string descripcion_reunion { get; set; }

        public string hora_reunion { get; set; }

        public DateTime fecha_reunion { get; set; }

        public int ID_semillero { get; set; }
    }
} 