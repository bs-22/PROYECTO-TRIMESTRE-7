using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace GestionSemillero1.Models
{
    [Table("actividad_proyecto")]
    public class ActividadProyecto
    {
        [Key]
        [Column("ID_activida_proyecto")] // Coincide con tu PK "ID_activida_proyecto"
        public decimal ID_activida_proyecto { get; set; }

        [Required]
        [Column("fecha_inicio_actividad_proyecto")]
        public DateTime fecha_inicio_actividad_proyecto { get; set; }

        [Required]
        [Column("fecha_fin_actividad_proyecto")]
        public DateTime fecha_fin_actividad_proyecto { get; set; }

        [Required]
        [StringLength(100)]
        // =========================================================================
        // CORRECCIÓN AQUÍ: Cambiar "descripcion_activity" por "descripcion_actividad"
        // =========================================================================
        [Column("descripcion_actividad_proyecto")]
        public string descripcion_actividad_proyecto { get; set; }

        [Required]
        [StringLength(45)]
        [Column("nombre_actividad_proyecto")]
        public string nombre_actividad_proyecto { get; set; }

        [Required]
        [Column("ID_fase_proyecto")]
        public decimal ID_fase_proyecto { get; set; }
    }
}