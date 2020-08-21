using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConorteClientAPI.Models
{
    class APLIQUIDACIONDET
    {
        public int EMPRESA { get; set; }
        public int SUCURSAL { get; set; }
        public string SERIE { get; set; }
        public string NUMERO { get; set; }
        public string CODIGOPRI { get; set; }
        public string NOMBRE { get; set; }
        public decimal PRECIO { get; set; }
        public decimal CANTIDAD { get; set; }
        public decimal SUBTOTAL { get; set; }
        public decimal DESCUENTO { get; set; }
        public decimal IVA { get; set; }
        public decimal NETO { get; set; }
        public int LINEA { get; set; }
    }
}
