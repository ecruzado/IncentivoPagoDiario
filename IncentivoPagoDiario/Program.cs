using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace IncentivoPagoDiario
{
    class Program
    {
        static void Main(string[] args)
        {
            Stopwatch relojGeneral = new Stopwatch();
            StringBuilder sbLog = new StringBuilder();

            sbLog.Append("Fecha Hora Procesamiento: ");
            sbLog.AppendLine(DateTime.Now.ToString());
            sbLog.AppendLine("");

            sbLog.AppendLine("Traer archivos de sftp: ");
            relojGeneral.Start();

            //Obtener Archivo del Servidor
            List<string> rutaArchivos = new List<string>();
            rutaArchivos.Add(@"E:\HTE - Edgar\Archivo de entrada\G20160516_01_9_309000.txt");
            //List<string> rutaArchivos = TraerArchivosDeSftp();
            
            relojGeneral.Stop();
            sbLog.Append("Tiempo de traer archivos: ");
            sbLog.AppendLine(relojGeneral.Elapsed.TotalSeconds.ToString());
            sbLog.AppendLine("");

            foreach (var rutaArchivo in rutaArchivos)
            {
                sbLog.AppendLine("Validar archivo: ");
                relojGeneral.Start();

                //Validar estructura
                var lista = ValidarArchivo(rutaArchivo, sbLog);

                relojGeneral.Stop();
                sbLog.Append("Tiempo de validar archivo: ");
                sbLog.AppendLine(relojGeneral.Elapsed.TotalSeconds.ToString());
                sbLog.AppendLine("");



                sbLog.AppendLine("Insertar datos: ");
                relojGeneral.Start();

                //Insertar a tablas 
                InsertarDatos(lista, 9);

                relojGeneral.Stop();
                sbLog.Append("Tiempo de insertar datos: ");
                sbLog.AppendLine(relojGeneral.Elapsed.TotalSeconds.ToString());
                sbLog.AppendLine("");



                sbLog.AppendLine("Llamar sp validar: ");
                relojGeneral.Start();

                //Validacion de datos
                LlamarSpValidar();

                relojGeneral.Stop();
                sbLog.Append("Tiempo de llamar sp validar: ");
                sbLog.AppendLine(relojGeneral.Elapsed.TotalSeconds.ToString());
                sbLog.AppendLine("");



                sbLog.AppendLine("Llamar sp cargar: ");
                relojGeneral.Start();

                //Poblar modelo de datos
                var listaProcesada = LlamarSpCargar();

                relojGeneral.Stop();
                sbLog.Append("Tiempo de llamar sp cargar: ");
                sbLog.AppendLine(relojGeneral.Elapsed.TotalSeconds.ToString());
                sbLog.AppendLine("");



                sbLog.AppendLine("Generar Archivo ganadores: ");
                relojGeneral.Start();

                //Generar archivo de ganadores 
                GenerarArchivoGanadores(listaProcesada);

                relojGeneral.Stop();
                sbLog.Append("Tiempo de generar archivo ganadores: ");
                sbLog.AppendLine(relojGeneral.Elapsed.TotalSeconds.ToString());
                sbLog.AppendLine("");



                //Notificar por correo
                NotificarPorCorreo("","");
            }


        }

        private static List<string> TraerArchivosDeSftp(StringBuilder log)
        {
            var listaArchivosObtenidos = new List<string>();

            try
            {
                string ftpHost = ConfigurationManager.AppSettings["ftpHost"];
                string ftpUsuario = ConfigurationManager.AppSettings["ftpUsuario"];
                string ftpClave = ConfigurationManager.AppSettings["ftpClave"];
                string ftpRuta = ConfigurationManager.AppSettings["ftpRuta"];

                string rutaDestino = ConfigurationManager.AppSettings["rutaDestino"];
                DateTime fecha = DateTime.Now;
                string nombreArchivoPatron = string.Format("G{0}", fecha.ToString("yyyyMMdd"));


                using (var client = new SftpClient(ftpHost, ftpUsuario, ftpClave))
                {
                    client.Connect();
                    Console.WriteLine("Connected to {0}", ftpHost);

                    client.ChangeDirectory(ftpRuta);
                    Console.WriteLine("Changed directory to {0}", ftpRuta);

                    var files = client.ListDirectory(ftpRuta);

                    foreach (var file in files)
                    {
                        if (file.Name.StartsWith(nombreArchivoPatron))
                        {
                            string nombreArchivo = string.Format("{0}\\{1}", rutaDestino, file.Name);

                            using (FileStream fs = new FileStream(nombreArchivo, FileMode.Open))
                            {
                                client.DownloadFile(file.FullName, fs);
                                listaArchivosObtenidos.Add(nombreArchivo);
                                log.AppendLine("Archivo obtenido: " + nombreArchivo);
                            }
                        }
                    }

                }
            }
            catch (Exception e)
            {
                log.AppendLine("Error en TraerArchivosDeSftp");
            }

            return listaArchivosObtenidos;

        }

        private static List<PagoDiarioTdp> ValidarArchivo(string rutaArchivo, StringBuilder log) 
        {
            List<PagoDiarioTdp> listaPagoDiario = new List<PagoDiarioTdp>();
            try
            {
                int numeroLinea = 1,
                    cabIdentificarIncetivo,
                    identificadorIncetivo;
                string lineaCabecera,
                    linea,
                    cabCodigoDistribuidor,
                    cabDescripcionIncentivo;
                bool exito;
                StringBuilder error = new StringBuilder();
                DateTime cabFechaIncioIncentivo, cabFechaFinIncentivo;
                decimal monto;
                PagoDiarioTdp pagoDiario;

                if (!File.Exists(rutaArchivo))
                {
                    log.AppendLine("Archivo no existe "+ rutaArchivo);
                    return null;
                }

                log.AppendLine("Inicio validacion archivo: " + rutaArchivo);
                using (StreamReader sr = new StreamReader(rutaArchivo))
                {
                    lineaCabecera = sr.ReadLine();
                    lineaCabecera = lineaCabecera.Trim();
                    string[] cabeceras = lineaCabecera.Split(Constantes.CARACTER_SEPARACION);
                    exito = true;

                    if (cabeceras.Length != Constantes.NUMERO_CAMPOS_CABECERA)
                    {
                        error.Append(string.Format(Constantes.MENSAJE_ERROR_NUMERO_CAMPOS_INCORRECTO,
                            numeroLinea, Constantes.NUMERO_CAMPOS_CABECERA));
                        exito = false;
                    }

                    if (exito)
                    {
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
                    log.AppendLine("Filas procesadas: " + numeroLinea);
                }
                log.AppendLine("Fin validacion archivo:" + rutaArchivo);
            }
            catch (Exception)
            {
                log.AppendLine("Error en ValidarArchivo");
            }
            return listaPagoDiario;
        }

        private static void InsertarDatos(List<PagoDiarioTdp> lista, int incentivoId) 
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
                }
                catch (Exception ex)
                {
                }
            }
        }

        private static bool LlamarSpValidar() 
        {
            string conexion = ConfigurationManager.ConnectionStrings["con"].ConnectionString;
            using (SqlConnection con = new SqlConnection(conexion)) 
            {
                using (SqlCommand cmd = new SqlCommand("SP_Validar"))
                {
                    cmd.Connection = con;
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.ExecuteNonQuery();
                }
            }
            return true;

        }

        private static List<PagoDiarioTdp> LlamarSpCargar()
        {
            string conexion = ConfigurationManager.ConnectionStrings["con"].ConnectionString;
            using (SqlConnection con = new SqlConnection(conexion))
            {
                using (SqlCommand cmd = new SqlCommand("SP_Cargar"))
                {
                    cmd.Connection = con;
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.ExecuteNonQuery();
                }
            }
            return null;

        }

        private static void GenerarArchivoGanadores(List<PagoDiarioTdp> lista) 
        {
            StringBuilder sb = new StringBuilder();
            PagoDiarioTdp pagoDiario = null;
            foreach (var item in lista)
            {
                sb.AppendLine(String.Format("{0},{1},{2},{3},{4:n2}", item.NumeroIncentivoTdp, item.DistribuidorTdp, item.CodigoPdvTdp, item.NumeroCelular, item.Monto));
                pagoDiario = item;
            }

            var nombre = String.Format("G{0}_{1}_{2}.csv", "", pagoDiario.NumeroIncentivoTdp, pagoDiario.DistribuidorTdp);
            var rutaSalida = ConfigurationManager.AppSettings["rutaSalida"];
            var nombreArchivo = string.Format("{0}//{1}", nombre, rutaSalida);

            File.WriteAllText(nombreArchivo, sb.ToString());

        }

        private static void NotificarPorCorreo(string titulo, string contenido) 
        {
            beMensaje obeMensaje = new beMensaje();
            string[] correos = ConfigurationManager.AppSettings["CorreoCargaMasiva"].ToString().Split(';');
            obeMensaje.De = ConfigurationManager.AppSettings["CorreoDe"].ToString();
            obeMensaje.Clave = ConfigurationManager.AppSettings["CorreoClave"].ToString();
            obeMensaje.Para = correos;
            obeMensaje.Asunto = titulo;
            obeMensaje.Contenido = contenido;
            Console.WriteLine("Enviando Email");
            ucCorreo.enviar(obeMensaje);

        }

        
        private static DataTable ToDataTable<T>(IList<T> data)
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
