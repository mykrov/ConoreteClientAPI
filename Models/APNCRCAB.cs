using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConorteClientAPI.Models
{
    class APNCRCAB
    {
        public int EMPRESA { get; set; }
        public int SUCURSAL { get; set; }
        public string SERIE { get; set; }
        public string NUMERO { get; set; }
        public string RUC { get; set; }
        public string TIPOIDENTIFICACION { get; set; }
        public string NOMBRE { get; set; }
        public string TIPO { get; set; }
        public string NUMEROFAC { get; set; }
        public string SERIEFAC { get; set; }
        public string DIRECCION { get; set; }
        public string TELEFONOS { get; set; }
        public string CORREO { get; set; }
        public string OBSERVACION { get; set; }
        public decimal SUBTOTALNCR { get; set; }
        public decimal SUBTOTALCERO { get; set; }
        public decimal DESCUENTONCR { get; set; }
        public decimal IVAFAC { get; set; }
        public decimal NETOFAC { get; set; }
        public string FECHAEMI { get; set; }
        public string FECHADOC { get; set; }
        public string ESMATRIZ { get; set; }
    }
}
