using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace GestionSemillero1.Models
{
    
    // Esta tabla actúa como tabla puente para gestionar la asistencia.
    [Table("asistencia_reunion")]
    public class AsistenciaReunion
    {
        [Key]
        [Column("ID_reunion", Order = 0)]
        public decimal ID_reunion { get; set; }

        [Key]
        [Column("ID_usuario", Order = 1)] 
        public decimal ID_usuario { get; set; }
    }
}