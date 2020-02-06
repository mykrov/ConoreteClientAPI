using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConorteClientAPI.Models
{
    class APRETENCION_DET
    {
        public int EMPRESA { get; set; }
        public int SUCURSAL { get; set; }
        public string SERIERET { get; set; }
        public long NUMERORET { get; set; }
        public string SERIE { get; set; }
        public long NUMERO { get; set; }
        public string TIPODOC { get; set; }
        public string FECHADOC { get; set; } 
        public decimal BASERET { get; set; }
        public decimal PORRET { get; set; }
        public decimal MONTORET { get; set; }
        public string CODRET { get; set; }
        public string TIPORET { get; set; }
    }
}
