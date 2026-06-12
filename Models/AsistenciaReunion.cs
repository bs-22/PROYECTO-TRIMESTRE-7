using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace GestionSemillero1.Models
{
    [Table("asistencia_reunion")]
    public class AsistenciaReunion
    {
        [Key]
        [Column("ID_reunion", Order = 0)]
        public decimal ID_reunion { get; set; }

        [Key]
        [Column("ID_usuario", Order = 1)] // <-- Cambiado a ID_usuario
        public decimal ID_usuario { get; set; }
    }
}