using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionSemillero1.Models
{
    [Table("reunion")]
    public class Reunion
    {
        [Key]
        [Column("ID_reunion")]
        public decimal ID_reunion { get; set; }

        [Required]
        [StringLength(100)]
        [Column("descripcion_reunion")]
        public string descripcion_reunion { get; set; }

        [Required]
        [StringLength(20)]
        [Column("hora_reunion")]
        public string hora_reunion { get; set; }

        [Required]
        [StringLength(20)]
        [Column("hora_fin_reunion")]
        public string hora_fin_reunion { get; set; }

        [Required]
        [StringLength(100)]
        [Column("lugar_reunion")]
        public string lugar_reunion { get; set; }

        [Required]
        [Column("fecha_reunion")]
        public DateTime fecha_reunion { get; set; }

        // CORRECCIÓN DEFINITIVA: Mapeado como decimal? para cumplir las exigencias de SqlClient
        [Column("ID_semillero")]
        public decimal ID_semillero { get; set; }

        [Column("estado_reunion")]
        public string estado_reunion { get; set; }

        // Añade esta línea para que Entity Framework sepa que hay una relación
        public virtual ICollection<AsistenciaReunion> AsistenciaReunion { get; set; }
    }
} 