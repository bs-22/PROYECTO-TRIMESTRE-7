using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace GestionSemillero1.Models
{
    public class UsuarioGestionViewModel
    {
        // Datos de la tabla 'investigadores'
        public decimal ID_investigador { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string nombre_investigador { get; set; }

        [Required(ErrorMessage = "El apellido es obligatorio")]
        public string apellido_investigador { get; set; }

        [Required(ErrorMessage = "El tipo de documento es obligatorio")]
        public string tipo_documento { get; set; }

        [Required(ErrorMessage = "La edad es obligatoria")]
        public int edad_investigador { get; set; }

        [Required(ErrorMessage = "El teléfono es obligatorio")]
        public decimal telefono_investigador { get; set; }

        public decimal ID_semillero { get; set; }

        // Datos de la tabla 'Usuario'
        public decimal ID_usuario { get; set; }

        [Required(ErrorMessage = "El estado es obligatorio")]
        public string estado_usuario { get; set; }

        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Correo inválido")]
        public string correo_usuario { get; set; }

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        public string contraseña_usuario { get; set; }

        [Required(ErrorMessage = "El rol/tipo es obligatorio")]
        public string tipo_usuario { get; set; } // Administrador, Investigador, Líder
    }

}