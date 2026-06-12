using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace GestionSemillero1.Models
{
    [Table("evento")]
    public class Evento
    {
        [Key]
        [Column("ID_evento")]
        public decimal ID_evento { get; set; }

        [Required]
        [Column("fecha_evento")]
        public DateTime fecha_evento { get; set; }

        [Required]
        [StringLength(45)]
        [Column("nombre_evento")]
        public string nombre_evento { get; set; }

        [Required]
        [StringLength(100)]
        [Column("descripción_evento")] // Mapeo explícito a la columna de la base de datos
        public string descripción_evento { get; set; }

        [Required]
        [Column("ID_semillero")]
        public decimal ID_semillero { get; set; }
    }
}