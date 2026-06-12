using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionSemillero1.Models
{
    // NOTA: Si tu tabla en la base de datos se llama en plural (por ejemplo, "Usuarios"), 
    // cambia "Usuario" por "Usuarios" dentro del paréntesis de abajo.
    [Table("Usuario")]
    public class Usuario
    {
        [Key]
        [Column("ID_usuario")]
        public decimal ID_usuario { get; set; } // El tipo 'numeric' de SQL equivale a 'decimal' en C#

        [Required]
        [StringLength(20)]
        [Column("estado_usuario")]
        public string estado_usuario { get; set; }

        [Required]
        [StringLength(45)]
        [Column("correo_usuario")]
        public string correo_usuario { get; set; }

        [Required]
        [StringLength(20)]
        [Column("contraseña_usuario")]
        public string contraseña_usuario { get; set; }

        [Required]
        [StringLength(20)]
        [Column("tipo_usuario")]
        public string tipo_usuario { get; set; }


    }
}