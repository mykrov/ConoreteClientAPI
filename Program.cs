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
        public static List<ArchivoXML> ret = new List<ArchivoXML>();
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
                    else if (typ == "07")
                    {
                        ret.Add(item_doc);
                    }
                    else if (typ == "04")
                    {
                        ncr.Add(item_doc);
                    }
                }

                facturas_proccess();
                retenciones_proccess();
                //Console.ReadKey();
            }

        }

        public static void facturas_proccess()
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
                                   // Console.WriteLine("Nueva Cabecera de Factura Insertada en APOLO. " + cab_factura.NUMERO);

                                    //···················##################### DETALLES DE FACTURA ##########################................................

                                    IList<JToken> detalles = json_obj["factura"]["detalles"]["detalle"].Children().ToList();
                                    int numLinea = 1;
                                    foreach (var detalle in detalles)
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
                                            det_factura.GRABAIVA = "S";
                                            det_factura.PORIVA = Convert.ToDecimal((decimal)detalle["impuestos"]["impuesto"]["tarifa"], cultureUS);
                                        }


                                        using (IDbConnection con2 = new SqlConnection(ConfigurationManager.ConnectionStrings["SEVERAPOLO"].ConnectionString))
                                        {
                                            //Revisar si ya ha sido insertada esa factura.
                                            string sqlExist2 = "SELECT COUNT(*) AS CONTADOR FROM APFACTURADET WHERE SERIE = '" + cab_factura.SERIE + "' AND NUMERO = '" + cab_factura.NUMERO + "' AND EMPRESA = " + cab_factura.EMPRESA + "AND LINEA = " + numLinea;
                                            dynamic result2 = con.Query(sqlExist).First();
                                            int contador2 = result.CONTADOR;
                                            if (contador == 0)
                                            {

                                                using (IDbConnection db32 = new SqlConnection(ConfigurationManager.ConnectionStrings["SEVERAPOLO"].ConnectionString))
                                                {
                                                    string querInsertDET = "INSERT INTO APFACTURADET(EMPRESA,SUCURSAL,SERIE,NUMERO,LINEA,CODIGOPRI,CODIGOSEC,NOMBREITEM,TIPOITEM," +
                                                        "CANTIDAD,PRECIO,SUBTOTAL,DESCUENTO,IVA,NETO,GRABAIVA,PORIVA,PORDES,DETALLE) VALUES(@EMPRESA,@SUCURSAL,@SERIE,@NUMERO,@LINEA," +
                                                        "@CODIGOPRI,@CODIGOSEC,@NOMBREITEM,@TIPOITEM,@CANTIDAD,@PRECIO,@SUBTOTAL,@DESCUENTO,@IVA,@NETO,@GRABAIVA,@PORIVA,@PORDES,@DETALLE)";
                                                    try
                                                    {
                                                        // Inserción de la cabecera de Factura.
                                                        var affectedRows2 = db32.Execute(querInsertDET, new
                                                        {
                                                            @EMPRESA = det_factura.EMPRESA,
                                                            @SUCURSAL = det_factura.SUCURSAL,
                                                            @SERIE = det_factura.SERIE,
                                                            @NUMERO = det_factura.NUMERO,
                                                            @LINEA = det_factura.LINEA,
                                                            @CODIGOPRI = det_factura.CODIGOPRI,
                                                            @CODIGOSEC = det_factura.CODIGOSEC,
                                                            @NOMBREITEM = det_factura.NOMBREITEM,
                                                            @TIPOITEM = det_factura.TIPOITEM,
                                                            @CANTIDAD = det_factura.CANTIDAD,
                                                            @PRECIO = det_factura.PRECIO,
                                                            @SUBTOTAL = det_factura.SUBTOTAL,
                                                            @DESCUENTO = det_factura.DESCUENTO,
                                                            @IVA = det_factura.IVA,
                                                            @NETO = det_factura.NETO,
                                                            @GRABAIVA = det_factura.GRABAIVA,
                                                            @PORIVA = det_factura.PORIVA,
                                                            @PORDES = det_factura.PORDES,
                                                            @DETALLE = "Detalle MasterWare"
                                                        });
                                                       // Console.WriteLine("Nuevo Detalle de Factura Insertada en APOLO. " + affectedRows2);
                                                        numLinea++;
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        throw e;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                               // Console.WriteLine("Detalle ya Existe en APOLO.");
                                                numLinea++;
                                            }
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    throw e;
                                }
                            }
                        }
                        else
                        {
                            //Console.WriteLine("Factura ya Existe en APOLO.");
                        }
                    }
                    //agregar cabecera al array.
                    //cabcerasFac.Add(cab_factura);
                    //Uri ul = new Uri("http://apiapolo.test:8088");
                    //postRequets(cabcerasFac, ul, "post");
                }
            }
        }

        public static void retenciones_proccess()
        {
            System.IFormatProvider cultureUS = new System.Globalization.CultureInfo("en-US");
            if (ret.Count > 0)
            {
                List<APFACTURACAB> cabcerasFac = new List<APFACTURACAB>();
                foreach (ArchivoXML item in ret)
                {
                    string xml_ret = item.XML;

                    XmlDocument doc_xml = new XmlDocument();
                    doc_xml.LoadXml(xml_ret);
                    string jsonText = JsonConvert.SerializeXmlNode(doc_xml);
                    JObject json_obj = JObject.Parse(jsonText);

                    APRETENCION cab_ret = new APRETENCION();
                    APRETENCION_DET det_ret = new APRETENCION_DET();

                    string rucEmpresa = (string)json_obj["comprobanteRetencion"]["infoTributaria"]["ruc"];

                    // Consultar Número de Empresa dependiendo el RUC.
                    using (IDbConnection db2 = new SqlConnection(ConfigurationManager.ConnectionStrings["SEVERAPOLO"].ConnectionString))
                    {
                        string queryEmpresa = "SELECT EMPRESA FROM APEMPRESA WHERE RUC = '" + rucEmpresa + "'";
                        dynamic row = db2.Query(queryEmpresa).First();
                        int empresa = Convert.ToInt32(row.EMPRESA);
                        cab_ret.EMPRESA = empresa;
                    }
                    cab_ret.SUCURSAL = 1;
                    string estab = (string)json_obj["comprobanteRetencion"]["infoTributaria"]["estab"];
                    string ptoEmi = (string)json_obj["comprobanteRetencion"]["infoTributaria"]["ptoEmi"];
                    long numero = (long)json_obj["comprobanteRetencion"]["infoTributaria"]["secuencial"];
                    
                    cab_ret.SERIERET = estab + ptoEmi;
                    cab_ret.NUMERORET = numero;
                    cab_ret.RUCRET = (string)json_obj["comprobanteRetencion"]["infoCompRetencion"]["identificacionSujetoRetenido"];
                    cab_ret.TIPOIDENTIFICACION = (string)json_obj["comprobanteRetencion"]["infoCompRetencion"]["tipoIdentificacionSujetoRetenido"];
                    cab_ret.NOMBRERET = (string)json_obj["comprobanteRetencion"]["infoCompRetencion"]["razonSocialSujetoRetenido"];
                    cab_ret.CODCLIENTE = cab_ret.RUCRET;
                    cab_ret.FECHARET = (string)json_obj["comprobanteRetencion"]["infoCompRetencion"]["fechaEmision"];
                    cab_ret.ESMATRIZ = "S";

                    string tipoDoc = (string)json_obj["comprobanteRetencion"]["infoTributaria"]["codDoc"];
                    string secuencial = (string)json_obj["comprobanteRetencion"]["infoTributaria"]["secuencial"];
                    string documentID = tipoDoc + "-" + estab + "-" + ptoEmi + "-" + secuencial;

                    cab_ret.DOCUMENTO_ID = documentID;

                    if (cab_ret.TIPOIDENTIFICACION == "04")
                    {
                        cab_ret.TIPOIDENTIFICACION = "R";
                    }
                    else if (cab_ret.TIPOIDENTIFICACION == "05")
                    {
                        cab_ret.TIPOIDENTIFICACION = "C";
                    }
                    else if (cab_ret.TIPOIDENTIFICACION == "07")
                    {
                        cab_ret.TIPOIDENTIFICACION = "F";
                    }
                    else
                    {
                        cab_ret.TIPOIDENTIFICACION = "0";
                    }


                    IList<JToken> camposAdi = json_obj["comprobanteRetencion"]["infoAdicional"]["campoAdicional"].Children().ToList();

                    foreach (JToken itemAdi in camposAdi)
                    {
                        if (itemAdi.Value<string>("@nombre") == "direccion")
                        {
                            cab_ret.DIRECCIONRET = (string)itemAdi["#text"];
                        }
                        else if (itemAdi.Value<string>("@nombre") == "telefono")
                        {
                            cab_ret.TELEFONRET = (string)itemAdi["#text"];
                        }
                        else if (itemAdi.Value<string>("@nombre") == "correo")
                        {
                            cab_ret.CORREORET = (string)itemAdi["#text"];
                        }
                    }

                    using (IDbConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["SEVERAPOLO"].ConnectionString))
                    {
                        //Revisar si ya ha sido insertada esa retención.
                        string sqlExist = "SELECT COUNT(*) AS CONTADOR FROM APRETENCION WHERE SERIERET = '" + cab_ret.SERIERET + "' AND NUMERORET = '" + cab_ret.NUMERORET + "' AND EMPRESA = " + cab_ret.EMPRESA;
                        dynamic result = con.Query(sqlExist).First();
                        int contador = result.CONTADOR;
                        if (contador == 0)
                        {

                            using (IDbConnection db3 = new SqlConnection(ConfigurationManager.ConnectionStrings["SEVERAPOLO"].ConnectionString))
                            {
                                string querInsertCAB = "INSERT INTO APRETENCION(EMPRESA,SUCURSAL,SERIERET,NUMERORET,RUCRET,TIPOIDENTIFICACION,NOMBRERET," +
                                    "CODCLIENTE,FECHARET,DIRECCIONRET,TELEFONRET,CORREORET,DOCUMENTO_ID,ESMATRIZ) VALUES(@EMPRESA,@SUCURSAL,@SERIERET,@NUMERORET," +
                                    "@RUCRET,@TIPOIDENTIFICACION,@NOMBRERET,@CODCLIENTE,@FECHARET,@DIRECCIONRET,@TELEFONRET,@CORREORET,@DOCUMENTO_ID,@ESMATRIZ)";
                                try
                                {
                                    // Inserción de la cabecera de Retencion.
                                    var affectedRows = db3.Execute(querInsertCAB, new
                                    {
                                        @EMPRESA = cab_ret.EMPRESA,
                                        @SUCURSAL = cab_ret.SUCURSAL,
                                        @SERIERET = cab_ret.SERIERET,
                                        @NUMERORET = cab_ret.NUMERORET,
                                        @RUCRET = cab_ret.RUCRET,
                                        @TIPOIDENTIFICACION = cab_ret.TIPOIDENTIFICACION,
                                        @NOMBRERET = cab_ret.NOMBRERET,
                                        @CODCLIENTE = cab_ret.CODCLIENTE,
                                        @FECHARET = cab_ret.FECHARET,
                                        @DIRECCIONRET = cab_ret.DIRECCIONRET,
                                        @TELEFONRET = cab_ret.TELEFONRET,
                                        @CORREORET = cab_ret.CORREORET,
                                        @DOCUMENTO_ID = cab_ret.DOCUMENTO_ID,
                                        @ESMATRIZ = cab_ret.ESMATRIZ
                                    });
                                    //Console.WriteLine("Nueva Cabecera de Retención Insertada en APOLO. " + cab_ret.NUMERORET);

                                    //···················##################### DETALLES DE RETENCIÓN ##########################.

                                    IList<JToken> detalles = json_obj["comprobanteRetencion"]["impuestos"]["impuesto"].Children().ToList();
                                   
                                    foreach (var detalle in detalles)
                                    {
                                        det_ret.EMPRESA = cab_ret.EMPRESA;
                                        det_ret.SUCURSAL = cab_ret.SUCURSAL;
                                        det_ret.SERIERET = cab_ret.SERIERET;
                                        det_ret.NUMERORET = numero;

                                        string seriedet = (string)detalle["numDocSustento"];

                                        det_ret.SERIE = seriedet.Substring(0, 6);
                                        det_ret.NUMERO = Convert.ToInt64(seriedet.Substring(7, 8));

                                        string tipoSustento = (string)detalle["codDocSustento"];

                                        if (tipoSustento == "01")
                                        {
                                            det_ret.TIPODOC = "FAC";
                                        }
                                        else if (tipoSustento == "03")
                                        {
                                            det_ret.TIPODOC = "LIC";
                                        }
                                        else if (tipoSustento == "04")
                                        {
                                            det_ret.TIPODOC = "NCR";
                                        }
                                        else if (tipoSustento == "05")
                                        {
                                            det_ret.TIPODOC = "NDB";
                                        }

                                        det_ret.FECHADOC = (string)detalle["fechaEmisionDocSustento"];
                                        det_ret.BASERET = (decimal)detalle["baseImponible"];
                                        det_ret.PORRET = (decimal)detalle["porcentajeRetener"];
                                        det_ret.MONTORET = (decimal)detalle["valorRetenido"];
                                        det_ret.CODRET = (string)detalle["codigoRetencion"];
                                        
                                        string tipoRetdet = (string)detalle["codigo"];
                                        if (tipoRetdet == "1")
                                        {
                                            det_ret.TIPORET = "RTF";
                                        }
                                        else if (tipoRetdet == "2")
                                        {
                                            det_ret.TIPORET = "RTI";
                                        }

                                        using (IDbConnection con2 = new SqlConnection(ConfigurationManager.ConnectionStrings["SEVERAPOLO"].ConnectionString))
                                        {
                                            //Revisar si ya ha sido insertada esa Det retencion.
                                            string sqlExist2 = "SELECT COUNT(*) AS CONTADOR FROM APRETENCION_DET WHERE CODRET = '" + det_ret.CODRET + "' AND NUMERORET = '" + det_ret.NUMERORET + "' AND EMPRESA = " + det_ret.EMPRESA;
                                            dynamic result2 = con.Query(sqlExist).First();
                                            int contador2 = result.CONTADOR;
                                            if (contador == 0)
                                            {
                                                using (IDbConnection db32 = new SqlConnection(ConfigurationManager.ConnectionStrings["SEVERAPOLO"].ConnectionString))
                                                {
                                                    string querInsertDET = "INSERT INTO  APRETENCION_DET(EMPRESA,SUCURSAL,SERIERET,NUMERORET,SERIE,NUMERO,TIPODOC," +
                                                        "FECHADOC,BASERET,PORRET,MONTORET,CODRET,TIPORET) VALUES(@EMPRESA,@SUCURSAL,@SERIERET,@NUMERORET," +
                                                        "@SERIE,@NUMERO,@TIPODOC,@FECHADOC,@BASERET,@PORRET,@MONTORET,@CODRET,@TIPORET)";
                                                    try
                                                    {
                                                        // Inserción de la detalle retención.
                                                        var affectedRows2 = db32.Execute(querInsertDET, new
                                                        {
                                                            @EMPRESA = det_ret.EMPRESA,
                                                            @SUCURSAL = det_ret.SUCURSAL,
                                                            @SERIERET = det_ret.SERIERET,
                                                            @NUMERORET = det_ret.NUMERORET,
                                                            @SERIE = det_ret.SERIE,
                                                            @NUMERO = det_ret.NUMERO,
                                                            @TIPODOC = det_ret.TIPODOC,
                                                            @FECHADOC = det_ret.FECHADOC,
                                                            @BASERET = det_ret.BASERET,
                                                            @PORRET = det_ret.PORRET,
                                                            @MONTORET = det_ret.MONTORET,
                                                            @CODRET = det_ret.CODRET,
                                                            @TIPORET = det_ret.TIPORET,
                                                        });
                                                        //Console.WriteLine("Nuevo Detalle de Retención Insertado en APOLO. " + affectedRows2);
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        throw e;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                               // Console.WriteLine("Detalle Retención ya Existe en APOLO.");
                                            }
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    throw e;
                                }
                            }
                        }
                        else
                        {
                           // Console.WriteLine("Retención ya Existe en APOLO.");
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
