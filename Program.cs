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
                        cab_factura.SUBTOTALCERO = Convert.ToSingle(0).ToString(cultureUS);
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
                                cab_factura.SUBTOTALCERO = Convert.ToSingle(searchResult.baseImponible, cultureUS).ToString(cultureUS);
                            }
                            else
                            {
                                cab_factura.IVAFAC = Convert.ToSingle(searchResult.valor, cultureUS).ToString(cultureUS);
                            }
                            searchResults.Add(searchResult);
                        }
                    }

                     
                    cab_factura.SERIE = estab + ptoEmi;
                    cab_factura.NUMERO = numero.ToString();
                    cab_factura.SUBTOTALFAC = Convert.ToSingle(subtotalfac).ToString(cultureUS);
                    cab_factura.DESCUENTOFAC = Convert.ToSingle(descuento).ToString(cultureUS);

                    cab_factura.NETOFAC = Convert.ToSingle((string)json_obj["factura"]["infoFactura"]["importeTotal"], cultureUS).ToString(cultureUS);
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
                    cab_factura.VENDEDOR = "VEND0001";
                    cab_factura.TIPO = "01";

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
