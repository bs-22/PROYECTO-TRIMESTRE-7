using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace GestionSemillero1.Models
{
    [Table("semillero")] // Nombre exacto de tu tabla en SQL
    public class semillero
    {
        [Key]
        [Column("ID_semillero")]
        public decimal ID_semillero { get; set; }

        [Required]
        [StringLength(45)]
        [Column("nombre_semillero")]
        public string nombre_semillero { get; set; }

        [Required]
        [Column("fecha_creacion_semillero")]
        public DateTime fecha_creacion_semillero { get; set; }

        [Required]
        [StringLength(255)]
        [Column("descripcion_semillero")]
        public string descripcion_semillero { get; set; }

        [Required]
        [StringLength(40)]
        [Column("linea_investigacion")]
        public string linea_investigacion { get; set; }

        [Required]
        [StringLength(20)]
        [Column("estado")]
        public string estado { get; set; }
    }
}