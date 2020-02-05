using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConorteClientAPI.Models
{
    class APFACTURADET
    {
        public string EMPRESA { get; set; }
        public string SUCURSAL { get; set; }
        public string SERIE { get; set; }
        public string NUMERO { get; set; }
        public string LINEA { get; set; }
        public string CODIGOPRI { get; set; }
        public string CODIGOSEC { get; set; }
        public string NOMBREITEM { get; set; }
        public string TIPOITEM { get; set; }
        public string CANTIDAD { get; set; }
        public decimal PRECIO { get; set; }
        public decimal SUBTOTAL { get; set; }
        public decimal DESCUENTO { get; set; }
        public decimal IVA { get; set; }
        public decimal NETO { get; set; }
        public string GRABAIVA { get; set; }
        public decimal PORIVA { get; set; }
        public decimal PORDES { get; set; }
    }
}
