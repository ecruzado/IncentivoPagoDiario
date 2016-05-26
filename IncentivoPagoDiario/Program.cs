using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IncentivoPagoDiario
{
    class Program
    {
        static void Main(string[] args)
        {
            //Obtener Archivo del Servidor
            List<string> rutaArchivos = new List<string>();
            rutaArchivos.Add(@"C:\prueba\G20160516_01_9_309000.txt");

            //Validar estructura
            foreach (var rutaArchivo in rutaArchivos)
            {
                var lista = Validar(rutaArchivo);
                InsertarDatos(lista, 9);
            }
            //Insertar a tablas 

            //Validacion de datos

            //Poblar modelo de datos

            //Generar archivo de ganadores 

            //Notificar por correo
        }

        static List<PagoDiarioTdp> Validar(string archivo) 
        {
            if (!File.Exists(archivo)) 
            {
                Console.WriteLine("no archivo");
                return null;
            }

            int numeroLinea = 1;
            string lineaCabecera, linea;
            bool exito;
            StringBuilder sbPdvTDP = new StringBuilder();
            StringBuilder error = new StringBuilder();
            DateTime cabFechaIncioIncentivo, cabFechaFinIncentivo;
            string cabCodigoDistribuidor;
            int cabIdentificarIncetivo, identificadorIncetivo;
            string cabDescripcionIncentivo;
            decimal monto;
            PagoDiarioTdp pagoDiario;
            List<PagoDiarioTdp> listaPagoDiario = new List<PagoDiarioTdp>();

            using (StreamReader sr = new StreamReader(archivo)) 
            {
                lineaCabecera = sr.ReadLine();
                lineaCabecera = lineaCabecera.Trim();
                string[] cabeceras = lineaCabecera.Split(Constantes.CARACTER_SEPARACION);
                if (cabeceras.Length != Constantes.NUMERO_CAMPOS_CABECERA) 
                {
                    error.Append(string.Format(Constantes.MENSAJE_ERROR_NUMERO_CAMPOS_INCORRECTO,
                        numeroLinea, Constantes.NUMERO_CAMPOS_CABECERA));
                    return null;
                }

                cabCodigoDistribuidor = cabeceras[Constantes.INDICE_CABECERA_CODIGO_DISTRIBUIDOR];
                if (string.IsNullOrWhiteSpace(cabCodigoDistribuidor)) 
                {
                    error.Append(string.Format(Constantes.MENSAJE_ERROR_CAMPO_VACIO,
                        Constantes.NOMBRE_CABECERA_CODIGO_DISTRIBUIDOR));
                }

                if (!int.TryParse(cabeceras[Constantes.INDICE_CABECERA_IDENTIFICADOR_INCENTIVO], 
                    out cabIdentificarIncetivo)) 
                {
                    error.Append(string.Format(Constantes.MENSAJE_ERROR_CAMPO_FORMATO,
                        Constantes.NOMBRE_CABECERA_IDENTIFICADOR_INCENTIVO));
                }

                cabDescripcionIncentivo = cabeceras[Constantes.INDICE_CABECERA_DESCRIPCION_INCENTIVO];
                if (string.IsNullOrWhiteSpace(cabDescripcionIncentivo))
                {
                    error.Append(string.Format(Constantes.MENSAJE_ERROR_CAMPO_VACIO,
                        Constantes.NOMBRE_CABECERA_DESCRIPCION_INCENTIVO));
                }

                if (!DateTime.TryParse(cabeceras[Constantes.INDICE_CABECERA_FECHA_INICIO_INCENTIVO], 
                    out cabFechaIncioIncentivo)) 
                {
                    error.Append(string.Format(Constantes.MENSAJE_ERROR_CAMPO_FORMATO,
                        Constantes.NOMBRE_CABECERA_FECHA_INICIO_INCENTIVO));
                }

                if (!DateTime.TryParse(cabeceras[Constantes.INDICE_CABECERA_FECHA_FIN_INCENTIVO], 
                    out cabFechaFinIncentivo))
                {
                    error.Append(string.Format(Constantes.MENSAJE_ERROR_CAMPO_FORMATO,
                        Constantes.NOMBRE_CABECERA_FECHA_FIN_INCENTIVO));
                }

                while (!sr.EndOfStream)
                {
                    numeroLinea++;
                    exito = true;

                    linea = sr.ReadLine();
                    linea = linea.Trim();
                    string[] campos = linea.Split(Constantes.CARACTER_SEPARACION);
                    if (campos.Length != Constantes.NUMERO_CAMPOS)
                    {
                        error.Append(string.Format(Constantes.MENSAJE_ERROR_NUMERO_CAMPOS_INCORRECTO,
                            numeroLinea, Constantes.NUMERO_CAMPOS));
                        exito = false;
                        continue;
                    }

                    pagoDiario = new PagoDiarioTdp();

                    if (!int.TryParse(campos[Constantes.INDICE_CAMPO_IDENTIFICADOR_INCENTIVO],
                        out identificadorIncetivo))
                    {
                        error.Append(string.Format(Constantes.MENSAJE_ERROR_CAMPO_FORMATO,
                            Constantes.NOMBRE_CAMPO_IDENTIFICADOR_INCENTIVO));
                        exito = false;
                    }
                    pagoDiario.NumeroIncentivoTdp = identificadorIncetivo;

                    pagoDiario.DistribuidorTdp = campos[Constantes.INDICE_CAMPO_CODIGO_DISTRIBUIDOR];
                    if (string.IsNullOrWhiteSpace(pagoDiario.DistribuidorTdp))
                    {
                        error.Append(string.Format(Constantes.MENSAJE_ERROR_CAMPO_VACIO,
                            Constantes.NOMBRE_CAMPO_CODIGO_DISTRIBUIDOR));
                        exito = false;
                    }

                    pagoDiario.CodigoPdvTdp = campos[Constantes.INDICE_CAMPO_CODIGO_PDV];
                    if (string.IsNullOrWhiteSpace(pagoDiario.CodigoPdvTdp))
                    {
                        error.Append(string.Format(Constantes.MENSAJE_ERROR_CAMPO_VACIO,
                            Constantes.NOMBRE_CAMPO_CODIGO_PDV));
                        exito = false;
                    }

                    pagoDiario.NumeroCelular = campos[Constantes.INDICE_CAMPO_CODIGO_CELULAR];
                    if (string.IsNullOrWhiteSpace(pagoDiario.NumeroCelular))
                    {
                        error.Append(string.Format(Constantes.MENSAJE_ERROR_CAMPO_VACIO,
                            Constantes.NOMBRE_CAMPO_CODIGO_CELULAR));
                        exito = false;
                    }

                    if (!decimal.TryParse(campos[Constantes.INDICE_CAMPO_MONTO],
                        out monto))
                    {
                        error.Append(string.Format(Constantes.MENSAJE_ERROR_CAMPO_FORMATO,
                            Constantes.NOMBRE_CAMPO_MONTO));
                        exito = false;
                    }
                    pagoDiario.Monto = monto;

                    if (exito) 
                    {
                        listaPagoDiario.Add(pagoDiario);
                    }
                }
            }
            return listaPagoDiario;
        }

        static void InsertarDatos(List<PagoDiarioTdp> lista, int incentivoId) 
        {
            var procesoPagoDiario = new ProcesoPagoDiario();
            procesoPagoDiario.IncentivoId = incentivoId;
            procesoPagoDiario.Fecha = DateTime.Today;
            procesoPagoDiario.FechaHoraCreacion = DateTime.Now;

            string conexion = ConfigurationManager.ConnectionStrings["con"].ConnectionString;
            using (SqlConnection con = new SqlConnection(conexion))
            {
                try
                {
                    con.Open();
                    using (SqlCommand cmd = new SqlCommand()) 
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "insert into ProcesoPagaDiario(Fecha, FechaHoraCreacion, IncentivoId) values (@Fecha, @FechaHoraCreacion, @IncentivoId); select SCOPE_IDENTITY();";
                        cmd.Parameters.Add("@Fecha", SqlDbType.DateTime).Value = procesoPagoDiario.Fecha;
                        cmd.Parameters.Add("@FechaHoraCreacion", SqlDbType.DateTime).Value = procesoPagoDiario.FechaHoraCreacion;
                        cmd.Parameters.Add("@IncentivoId", SqlDbType.Int).Value = procesoPagoDiario.IncentivoId;

                        procesoPagoDiario.ProcesoPagoDiarioId = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    lista.ForEach(x => x.ProcesoPagoDiarioId = procesoPagoDiario.ProcesoPagoDiarioId);
                    var tabla = ToDataTable(lista);
                    Console.WriteLine("Insertando Tabla: ");
                    using (SqlBulkCopy sbc = new SqlBulkCopy(con))
                    {
                        sbc.BulkCopyTimeout = 200000;
                        sbc.DestinationTableName = "PagaDiarioTDP";
                        sbc.WriteToServer(tabla);
                    }
                    //sbLog.Append("Tabla PagaDiarioTDP - Registros Copiados: ");
                    //sbLog.AppendLine(tblPdvs.Rows.Count.ToString());
                    //sbLog.AppendLine("");
                }
                catch (Exception ex)
                {
                }
            }
        }

        public static DataTable ToDataTable<T>(IList<T> data)
        {
            PropertyDescriptorCollection props =
                TypeDescriptor.GetProperties(typeof(T));
            DataTable table = new DataTable();
            for (int i = 0; i < props.Count; i++)
            {
                PropertyDescriptor prop = props[i];
                table.Columns.Add(prop.Name, prop.PropertyType);
            }
            object[] values = new object[props.Count];
            foreach (T item in data)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = props[i].GetValue(item);
                }
                table.Rows.Add(values);
            }
            return table;
        }

    }

    public class ProcesoPagoDiario 
    {
        public int ProcesoPagoDiarioId { get; set; }
        public DateTime Fecha { get; set; }
        public DateTime FechaHoraCreacion { get; set; }
        public int IncentivoId { get; set; }
    }

    public class PagoDiarioTdp 
    {
        public int PagoDiarioTdpId { get; set; }
        public int NumeroIncentivoTdp { get; set; }
        public string DistribuidorTdp { get; set; }
        public string CodigoPdvTdp { get; set; }
        public string NumeroCelular { get; set; }
        public decimal Monto { get; set; }
        public string Estado { get; set; }
        public string Observacion { get; set; }
        public int ProcesoPagoDiarioId { get; set; }
    }
}
