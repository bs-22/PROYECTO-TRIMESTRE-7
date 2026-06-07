using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace GestionSemillero1.Models
{
    [Table("fase_proyecto")]
    public class FaseProyecto
    {
        [Key]
        [Column("ID_fase_proyecto")]
        public decimal ID_fase_proyecto { get; set; }

        [Required]
        [StringLength(45)]
        [Column("nombre_fase_proyecto")]
        public string nombre_fase_proyecto { get; set; }

        [Required]
        [StringLength(100)]
        [Column("descripcion_fase_proyecto")]
        public string descripcion_fase_proyecto { get; set; }

        [Required]
        [Column("ID_proyecto")]
        public decimal ID_proyecto { get; set; }
    }
}