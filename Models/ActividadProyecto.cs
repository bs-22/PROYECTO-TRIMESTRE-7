using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace GestionSemillero1.Models
{
    // Define el mapeo de la clase con la tabla física 'actividad_proyecto' en la base de datos.
    [Table("actividad_proyecto")]
    public class ActividadProyecto
    {
        // Clave primaria: Identificador único de la actividad.
        [Key]
        [Column("ID_activida_proyecto")]
        public decimal ID_activida_proyecto { get; set; }

        // Fechas de cronograma para el control de cumplimiento de la actividad.
        [Required]
        [Column("fecha_inicio_actividad_proyecto")]
        public DateTime fecha_inicio_actividad_proyecto { get; set; }

        [Required]
        [Column("fecha_fin_actividad_proyecto")]
        public DateTime fecha_fin_actividad_proyecto { get; set; }

        // Descripción detallada: Limitada a 100 caracteres por restricciones de la tabla.
        [Required]
        [StringLength(100)]
        [Column("descripcion_actividad_proyecto")]
        public string descripcion_actividad_proyecto { get; set; }

        // Nombre de la actividad: Limitado a 45 caracteres.
        [Required]
        [StringLength(45)]
        [Column("nombre_actividad_proyecto")]
        public string nombre_actividad_proyecto { get; set; }

        // Clave foránea: Relaciona esta actividad con una fase específica del proyecto.
        [Required]
        [Column("ID_fase_proyecto")]
        public decimal ID_fase_proyecto { get; set; }
    }
}