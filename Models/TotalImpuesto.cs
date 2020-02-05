using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConorteClientAPI.Models
{
    class TotalImpuesto
    {
        public int codigo { get; set; }
        public decimal codigoPorcentaje { get; set; }
        public decimal baseImponible { get; set; }
        public decimal  valor { get; set; }
    }
}
