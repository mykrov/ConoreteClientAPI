using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using ConorteClientAPI.Models;
using Dapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ConorteClientAPI
{
    class Program
    {
        public static List<ArchivoXML> facturas = new List<ArchivoXML>();
        public static List<ArchivoXML> liq = new List<ArchivoXML>();
        public static List<ArchivoXML> ncr = new List<ArchivoXML>();
        static void Main(string[] args)
        {
            List<Notificacion> notifications = new List<Notificacion>();
            List<ArchivoXML> documents = new List<ArchivoXML>();

            using (IDbConnection db = new SqlConnection(ConfigurationManager.ConnectionStrings["SEVERCONORTE"].ConnectionString))
            {
                documents = db.Query<ArchivoXML>("AP_NO_PROCESADO", commandType: CommandType.StoredProcedure).ToList();

                foreach (ArchivoXML item_doc in documents)
                {
                    string typ = item_doc.tipo_documento;
                    if (typ == "01")
                    {
                        facturas.Add(item_doc);
                    }
                }
                facturas_api();
                Console.ReadKey();
            }

        }

        public static void facturas_api()
        {
            System.IFormatProvider cultureUS = new System.Globalization.CultureInfo("en-US");
            if (facturas.Count > 0)
            {
                List<APFACTURACAB> cabcerasFac = new List<APFACTURACAB>();
                foreach (ArchivoXML item in facturas)
                {
                    string xml_fac = item.XML;

                    XmlDocument doc_xml = new XmlDocument();
                    doc_xml.LoadXml(xml_fac);
                    string jsonText = JsonConvert.SerializeXmlNode(doc_xml);
                    JObject json_obj = JObject.Parse(jsonText);

                    APFACTURACAB cab_factura = new APFACTURACAB();
                    APFACTURADET det_factura = new APFACTURADET();

                    string rucEmpresa = (string)json_obj["factura"]["infoTributaria"]["ruc"];
                    
                    // Consultar Número de Empresa dependiendo el RUC.
                    using (IDbConnection db2 = new SqlConnection(ConfigurationManager.ConnectionStrings["SEVERAPOLO"].ConnectionString))
                    {
                        string queryEmpresa = "SELECT EMPRESA FROM APEMPRESA WHERE RUC = '" + rucEmpresa + "'";
                        dynamic row = db2.Query(queryEmpresa).First();
                        int empresa = Convert.ToInt32(row.EMPRESA);
                        cab_factura.EMPRESA = empresa;
                    }
                    cab_factura.SUCURSAL = 1;
                    string estab = (string)json_obj["factura"]["infoTributaria"]["estab"];
                    string ptoEmi = (string)json_obj["factura"]["infoTributaria"]["ptoEmi"];
                    long numero = (long)json_obj["factura"]["infoTributaria"]["secuencial"];
                    decimal subtotalfac = (decimal)json_obj["factura"]["infoFactura"]["totalSinImpuestos"];
                    decimal descuento = (decimal)json_obj["factura"]["infoFactura"]["totalDescuento"];

                    int count = (int)json_obj["factura"]["infoFactura"]["totalConImpuestos"].Count();

                    if (count == 1)
                    {
                        cab_factura.SUBTOTALCERO = 0;
                        cab_factura.IVAFAC = (decimal)json_obj["factura"]["infoFactura"]["totalConImpuestos"]["totalImpuesto"]["valor"];
                    }
                    else
                    {
                        IList<JToken> results = json_obj["factura"]["infoFactura"]["totalConImpuestos"]["totalImpuesto"].Children().ToList();

                        // serialize JSON results into .NET objects
                        IList<TotalImpuesto> searchResults = new List<TotalImpuesto>();
                        foreach (JToken result in results)
                        {
                            // JToken.ToObject is a helper method that uses JsonSerializer internally
                            TotalImpuesto searchResult = result.ToObject<TotalImpuesto>();
                            if (searchResult.codigoPorcentaje == 0)
                            {
                                cab_factura.SUBTOTALCERO = Convert.ToDecimal(searchResult.baseImponible, cultureUS);
                            }
                            else
                            {
                                cab_factura.IVAFAC = Convert.ToDecimal(searchResult.valor, cultureUS);
                            }
                            searchResults.Add(searchResult);
                        }
                    }

                     
                    cab_factura.SERIE = estab + ptoEmi;
                    cab_factura.NUMERO = numero.ToString();
                    cab_factura.SUBTOTALFAC = Convert.ToDecimal(subtotalfac);
                    cab_factura.DESCUENTOFAC = Convert.ToDecimal(descuento);
                    cab_factura.NETOFAC = Convert.ToDecimal((decimal)json_obj["factura"]["infoFactura"]["importeTotal"], cultureUS);
                    cab_factura.FECHAEMI = (string)json_obj["factura"]["infoFactura"]["fechaEmision"];
                    cab_factura.FECHAVEN = cab_factura.FECHAEMI;
                    cab_factura.RUC = (string)json_obj["factura"]["infoFactura"]["identificacionComprador"];
                    cab_factura.TIPOIDENTIFICACION = (string)json_obj["factura"]["infoFactura"]["tipoIdentificacionComprador"];
                    cab_factura.NOMBRE = (string)json_obj["factura"]["infoFactura"]["razonSocialComprador"];
                    cab_factura.CORREO = "test@gmail.com";
                    cab_factura.TELEFONOS = "";
                    cab_factura.DIRECCION = "";
                    IList<JToken> camposAdi = json_obj["factura"]["infoAdicional"]["campoAdicional"].Children().ToList();

                    foreach (JToken itemAdi in camposAdi)
                    {
                        if (itemAdi.Value<string>("@nombre") == "direccion")
                        {
                            cab_factura.DIRECCION = (string)itemAdi["#text"];
                        }
                        else if (itemAdi.Value<string>("@nombre") == "telefono")
                        {
                            cab_factura.TELEFONOS = (string)itemAdi["#text"];
                        }
                        else if (itemAdi.Value<string>("@nombre") == "correo")
                        {
                            cab_factura.CORREO = (string)itemAdi["#text"];
                        }
                    }

                    cab_factura.OBSERVACION = "Factura desde MasterWare";
                    cab_factura.FORMAPAGO = (string)json_obj["factura"]["infoFactura"]["pagos"]["pago"]["formaPago"];
                    cab_factura.PLAZO = (string)json_obj["factura"]["infoFactura"]["pagos"]["pago"]["plazo"]; ;
                    cab_factura.CODCLIENTE = (string)json_obj["factura"]["infoFactura"]["identificacionComprador"];
                    cab_factura.VENDEDOR = "VEN1";
                    cab_factura.TIPO = "01";

                    // Cambio de los código de tipos de identificación a los de APOLO.
                    if (cab_factura.TIPOIDENTIFICACION == "04")
                    {
                        cab_factura.TIPOIDENTIFICACION = "R";
                    }
                    else if(cab_factura.TIPOIDENTIFICACION == "05")
                    {
                        cab_factura.TIPOIDENTIFICACION = "C";
                    }
                    else if(cab_factura.TIPOIDENTIFICACION == "07")
                    {
                        cab_factura.TIPOIDENTIFICACION = "F";
                    }
                    else
                    {
                        cab_factura.TIPOIDENTIFICACION = "0";
                    }

                    string secuencial = (string)json_obj["factura"]["infoTributaria"]["secuencial"];

                    string documentID = cab_factura.TIPO + "-" + estab + "-" + ptoEmi + "-" + secuencial;

                    //···················##################### DETALLES DE FACTURA ##########################................................

                    //int countDet = (int)json_obj["factura"]["detalles"].Count();

                    IList<JToken> detalles = json_obj["factura"]["detalles"]["detalle"].Children().ToList();
                    int numLinea = 1;
                    foreach (var detalle  in detalles)
                    {
                        det_factura.EMPRESA = cab_factura.EMPRESA;
                        det_factura.SUCURSAL = 1;
                        det_factura.SERIE = cab_factura.SERIE;
                        det_factura.NUMERO = cab_factura.NUMERO;
                        det_factura.LINEA = numLinea;
                        det_factura.CODIGOPRI = (string)detalle["codigoPrincipal"].ToString();
                        det_factura.CODIGOSEC = (string)detalle["codigoPrincipal"].ToString();
                        det_factura.NOMBREITEM = (string)detalle["descripcion"].ToString();
                        det_factura.TIPOITEM = "B";
                        det_factura.CANTIDAD = (string)detalle["cantidad"].ToString();
                        det_factura.PRECIO = Convert.ToDecimal((decimal)detalle["precioUnitario"], cultureUS);
                        det_factura.SUBTOTAL = Convert.ToDecimal((decimal)detalle["precioTotalSinImpuesto"], cultureUS);
                        det_factura.DESCUENTO = Convert.ToDecimal((decimal)detalle["descuento"], cultureUS);
                        det_factura.PORDES = (decimal)detalle["descuento"];
                        
                        if (Convert.ToInt32((Int32)detalle["impuestos"]["impuesto"]["codigo"], cultureUS) == 0)
                        {
                            det_factura.IVA = 0;
                            det_factura.GRABAIVA = "N";
                            det_factura.PORIVA = 0;
                            det_factura.NETO = det_factura.SUBTOTAL;
                        }
                        else
                        {
                            det_factura.IVA = Convert.ToDecimal((decimal)detalle["impuestos"]["impuesto"]["valor"], cultureUS);
                            det_factura.NETO = det_factura.SUBTOTAL + det_factura.IVA;
                            det_factura.GRABAIVA = "S" ;
                            det_factura.PORIVA = Convert.ToDecimal((decimal)detalle["impuestos"]["impuesto"]["tarifa"], cultureUS);
                        }
                        numLinea++; 


                    }


                    using (IDbConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["SEVERAPOLO"].ConnectionString))
                    {
                        //Revisar si ya ha sido insertada esa factura.
                        string sqlExist = "SELECT COUNT(*) AS CONTADOR FROM APFACTURACAB WHERE SERIE = '" + cab_factura.SERIE + "' AND NUMERO = '" + cab_factura.NUMERO + "' AND EMPRESA = " + cab_factura.EMPRESA;
                        dynamic result = con.Query(sqlExist).First();
                        int contador = result.CONTADOR;
                        if (contador == 0) 
                        {
                            
                            using (IDbConnection db3 = new SqlConnection(ConfigurationManager.ConnectionStrings["SEVERAPOLO"].ConnectionString))
                            {
                                string querInsertCAB = "INSERT INTO APFACTURACAB(EMPRESA,SUCURSAL,SERIE,NUMERO,SUBTOTALFAC,SUBTOTALCERO,DESCUENTOFAC," +
                                    "IVAFAC,NETOFAC,FECHAEMI,FECHAVEN,RUC,TIPOIDENTIFICACION,NOMBRE,DIRECCION,TELEFONOS,CORREO,OBSERVACION,FORMAPAGO," +
                                    "PLAZO,CODCLIENTE,VENDEDOR,TIPO,DOCUMENTO_ID,ESMATRIZ,CLAVE) VALUES(@EMPRESA,@SUCURSAL,@SERIE,@NUMERO,@SUBTOTALFAC,@SUBTOTALCERO,@DESCUENTOFAC," +
                                    "@IVAFAC,@NETOFAC,@FECHAEMI,@FECHAVEN,@RUC,@TIPOIDENTIFICACION,@NOMBRE,@DIRECCION,@TELEFONOS,@CORREO,@OBSERVACION," +
                                    "@FORMAPAGO,@PLAZO,@CODCLIENTE,@VENDEDOR,@TIPO,@DOCUMENTO_ID,'S',@CLAVE)";
                                try
                                {
                                    // Inserción de la cabecera de Factura.
                                    var affectedRows = db3.Execute(querInsertCAB, new
                                    {
                                        @EMPRESA = cab_factura.EMPRESA,
                                        @SUCURSAL = cab_factura.SUCURSAL,
                                        @SERIE = cab_factura.SERIE,
                                        @NUMERO = cab_factura.NUMERO,
                                        @SUBTOTALFAC = cab_factura.SUBTOTALFAC,
                                        @SUBTOTALCERO = cab_factura.SUBTOTALCERO,
                                        @DESCUENTOFAC = cab_factura.DESCUENTOFAC,
                                        @IVAFAC = cab_factura.IVAFAC,
                                        @NETOFAC = cab_factura.NETOFAC,
                                        @FECHAEMI = cab_factura.FECHAEMI,
                                        @FECHAVEN = cab_factura.FECHAVEN,
                                        @RUC = cab_factura.RUC,
                                        @TIPOIDENTIFICACION = cab_factura.TIPOIDENTIFICACION,
                                        @NOMBRE = cab_factura.NOMBRE,
                                        @DIRECCION = cab_factura.DIRECCION,
                                        @TELEFONOS = cab_factura.TELEFONOS,
                                        @CORREO = cab_factura.CORREO,
                                        @OBSERVACION = cab_factura.OBSERVACION,
                                        @FORMAPAGO = cab_factura.FORMAPAGO,
                                        @PLAZO = cab_factura.PLAZO,
                                        @CODCLIENTE = cab_factura.CODCLIENTE,
                                        @VENDEDOR = cab_factura.VENDEDOR,
                                        @TIPO = cab_factura.TIPO,
                                        @DOCUMENTO_ID = documentID,
                                        @CLAVE = cab_factura.CODCLIENTE
                                    });
                                    Console.WriteLine("Nueva Cabecera de Factura Insertada en APOLO." +affectedRows);
                                }
                                catch (Exception e)
                                {
                                    throw e;
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("Factura ya Existe en APOLO.");
                        }
                    }
                    //agregar cabecera al array.
                    //cabcerasFac.Add(cab_factura);
                    //Uri ul = new Uri("http://apiapolo.test:8088");
                    //postRequets(cabcerasFac, ul, "post");
                }
            }
        }

        public static void postRequets(List<APFACTURACAB> lista, Uri endpoint, string metodo)
        {

            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string jsonString = JsonConvert.SerializeObject(lista, new FormattedDecimalConverter(CultureInfo.GetCultureInfo("en-US")));

            var http = (HttpWebRequest)WebRequest.Create(new Uri(endpoint + "api/cabfac"));
            http.Accept = "application/json";
            http.ContentType = "application/json";
            http.Method = "POST";

            string parsedContent = jsonString;
            ASCIIEncoding encoding = new ASCIIEncoding();
            Byte[] bytes = encoding.GetBytes(parsedContent);

            Stream newStream = http.GetRequestStream();
            newStream.Write(bytes, 0, bytes.Length);
            newStream.Close();

            var response = http.GetResponse();

            var stream = response.GetResponseStream();
            var sr = new StreamReader(stream);
            var content = sr.ReadToEnd();

            Console.WriteLine(content);

        }
    }


    class FormattedDecimalConverter : JsonConverter
    {
        private CultureInfo culture;

        public FormattedDecimalConverter(CultureInfo culture)
        {
            this.culture = culture;
        }

        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(decimal) ||
                    objectType == typeof(double) ||
                    objectType == typeof(float));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(Convert.ToString(value, culture));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
