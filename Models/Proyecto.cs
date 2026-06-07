using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace GestionSemillero1.Models
{
    [Table("proyecto")]
    public class Proyecto
    {
        [Key]
        [Column("ID_proyecto")]
        public decimal ID_proyecto { get; set; }

        [Required]
        [StringLength(45)]
        [Column("nombre_proyecto")]
        public string nombre_proyecto { get; set; }

        [Required]
        [StringLength(45)]
        [Column("actividad_proyecto")]
        public string actividad_proyecto { get; set; }

        [Required]
        [Column("fecha_inicio_proyecto")]
        public DateTime fecha_inicio_proyecto { get; set; }

        [Required]
        [Column("fecha_fin_proyecto")]
        public DateTime fecha_fin_proyecto { get; set; }

        [Required]
        [Column("ID_semillero")]
        public decimal ID_semillero { get; set; }
    }
}