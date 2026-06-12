using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace GestionSemillero1.Models
{
    [Table("investigadores")]
    public class investigadores
    {
        [Key]
        [Column("ID_investigador")]
        public decimal ID_investigador { get; set; }

        [Required]
        [StringLength(45)]
        [Column("nombre_investigador")]
        public string nombre_investigador { get; set; }

        [Required]
        [StringLength(30)]
        [Column("apellido_investigador")]
        public string apellido_investigador { get; set; }

        [Required]
        [StringLength(45)]
        [Column("tipo_documento")]
        public string tipo_documento { get; set; }

        [Required]
        [Column("edad_investigador")]
        public int edad_investigador { get; set; }

        [Required]
        [Column("telefono_investigador")]
        public decimal telefono_investigador { get; set; }

        [Required]
        [Column("ID_usuario")]
        public decimal ID_usuario { get; set; }

        [Required]
        [Column("ID_semillero")]
        public decimal ID_semillero { get; set; }

        // ====== AGREGA ESTAS LÍNEAS AL FINAL ======
        [ForeignKey("ID_usuario")]
        public virtual Usuario Usuario { get; set; }

        [ForeignKey("ID_semillero")]
        public virtual semillero semillero { get; set; }

    }
}