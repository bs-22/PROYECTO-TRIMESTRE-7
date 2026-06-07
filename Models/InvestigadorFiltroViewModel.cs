using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace GestionSemillero1.Models
{
    public class InvestigadorFiltroViewModel
    {
        public decimal ID_investigador { get; set; }
        public string NombreCompleto { get; set; }
        public string correo_usuario { get; set; }
        public string estado_usuario { get; set; }
        public decimal ID_usuario { get; set; }
    }
}