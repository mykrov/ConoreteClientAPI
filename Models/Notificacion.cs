using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConorteClientAPI.Models
{
    class Notificacion
    {
        public string tipo_documento { get; set; }
        public string documento { get; set; }
        public string estado_documento { get; set; }
        public string descripcion_estado_documento { get; set; }
        public string autorizacion { get; set; }
        public string clave { get; set; }
        public string codigo_SRI { get; set; }
        public string comentario_notificacion { get; set; }
        public string documento_id { get; set; }

    }
}
