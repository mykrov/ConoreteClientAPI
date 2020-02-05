﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConorteClientAPI.Models
{
    class APFACTURACAB
    {
        public int EMPRESA { get; set; }
        public int SUCURSAL { get; set; }
        public string SERIE { get; set; }
        public string NUMERO { get; set; }
        public string SUBTOTALFAC { get; set; }
        public string SUBTOTALCERO { get; set; }
        public string DESCUENTOFAC { get; set; }
        public string IVAFAC { get; set; }
        public string NETOFAC { get; set; }
        public string FECHAEMI { get; set; }
        public string FECHAVEN { get; set; }
        public string RUC { get; set; }
        public string TIPOIDENTIFICACION { get; set; }
        public string NOMBRE { get; set; }
        public string DIRECCION { get; set; }
        public string TELEFONOS { get; set; }
        public string CORREO { get; set; }
        public string OBSERVACION { get; set; }
        public string FORMAPAGO { get; set; }
        public string PLAZO { get; set; }
        public string CODCLIENTE { get; set; }
        public string VENDEDOR { get; set; }
        public string TIPO { get; set; }
    }
}
