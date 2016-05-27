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
using System.Linq;

namespace IncentivoPagoDiario
{
    class Program
    {
        static void Main(string[] args)
        {
            Stopwatch relojGeneral = new Stopwatch();
            StringBuilder sbLog = new StringBuilder();
            string cadenaConexion = ConfigurationManager.ConnectionStrings["con"].ConnectionString;
            DateTime fechaProceso = DateTime.Today;

            sbLog.Append("Fecha Hora Procesamiento: ");
            sbLog.AppendLine(DateTime.Now.ToString());
            sbLog.AppendLine("");

            sbLog.AppendLine("Traer archivos de sftp: ");
            relojGeneral.Start();

            //Obtener Archivo del Servidor
            List<string> rutaArchivos = new List<string>();
            rutaArchivos.Add(@"C:\prueba\G20160516_01_9_309000.txt");
            //List<string> rutaArchivos = TraerArchivosDeSftp(fechaProceso, sbLog);

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

                if (lista == null || lista.Count == 0)
                    continue;

                SqlTransaction trx = null;
                using (SqlConnection cnn = new SqlConnection(cadenaConexion))
                {
                    try
                    {
                        cnn.Open();
                        trx = cnn.BeginTransaction();

                        sbLog.AppendLine("Insertar datos: ");
                        relojGeneral.Start();

                        //Insertar a tablas 
                        InsertarDatos(trx, cnn, fechaProceso, lista, sbLog);

                        relojGeneral.Stop();
                        sbLog.Append("Tiempo de insertar datos: ");
                        sbLog.AppendLine(relojGeneral.Elapsed.TotalSeconds.ToString());
                        sbLog.AppendLine("");



                        sbLog.AppendLine("Llamar UspValidarPagoDiario: ");
                        relojGeneral.Start();

                        //Validacion de datos
                        if (LlamarUspValidarPagoDiario(trx, cnn, fechaProceso, rutaArchivo, sbLog))
                        {
                            trx.Commit();
                        }
                        else
                        {
                            trx.Rollback();
                        }


                        relojGeneral.Stop();
                        sbLog.Append("Tiempo de llamar UspValidarPagoDiario: ");
                        sbLog.AppendLine(relojGeneral.Elapsed.TotalSeconds.ToString());
                        sbLog.AppendLine("");
                    }
                    catch (Exception)
                    {
                        trx.Rollback();
                    }
                }

                sbLog.AppendLine("Llamar a UspCargarPagaDiario: ");
                relojGeneral.Start();

                //Poblar modelo de datos
                var listaProcesada = LlamarUspCargarPagaDiario(fechaProceso, sbLog);

                relojGeneral.Stop();
                sbLog.Append("Tiempo de llamar a UspCargarPagaDiario: ");
                sbLog.AppendLine(relojGeneral.Elapsed.TotalSeconds.ToString());
                sbLog.AppendLine("");



                sbLog.AppendLine("Generar Archivo ganadores: ");
                relojGeneral.Start();

                //Generar archivo de ganadores 
                GenerarArchivoGanadores(listaProcesada, sbLog);

                relojGeneral.Stop();
                sbLog.Append("Tiempo de generar archivo ganadores: ");
                sbLog.AppendLine(relojGeneral.Elapsed.TotalSeconds.ToString());
                sbLog.AppendLine("");



            }

            //Notificar por correo
            NotificarPorCorreo("Incentivo Pago Diario " + fechaProceso.ToShortDateString(), sbLog.ToString());

        }

        private static List<string> TraerArchivosDeSftp(DateTime fecha, StringBuilder log)
        {
            var listaArchivosObtenidos = new List<string>();

            try
            {
                string ftpHost = ConfigurationManager.AppSettings["ftpHost"];
                string ftpUsuario = ConfigurationManager.AppSettings["ftpUsuario"];
                string ftpClave = ConfigurationManager.AppSettings["ftpClave"];
                string ftpRuta = ConfigurationManager.AppSettings["ftpRuta"];

                string rutaEntrada = ConfigurationManager.AppSettings["rutaEntrada"];
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
                        Console.WriteLine(file.Name);

                        if (file.Name.StartsWith(nombreArchivoPatron))
                        {
                            string nombreArchivo = string.Format("{0}\\{1}", rutaEntrada, file.Name);

                            using (FileStream fs = new FileStream(nombreArchivo, FileMode.OpenOrCreate))
                            {
                                client.DownloadFile(file.FullName, fs);
                                listaArchivosObtenidos.Add(nombreArchivo);
                                log.AppendLine("Archivo obtenido: " + nombreArchivo);
                                Console.WriteLine("Archivo obtenido: " + nombreArchivo);

                            }
                        }
                    }

                }
            }
            catch (Exception e)
            {
                log.AppendLine("Error en TraerArchivosDeSftp");
                Console.WriteLine(e.Message);                
                Console.WriteLine(e.StackTrace);

            }

            return listaArchivosObtenidos;

        }

        private static List<PagoDiarioTdp> ValidarArchivo(string rutaArchivo, StringBuilder log) 
        {
            List<PagoDiarioTdp> listaPagoDiario = new List<PagoDiarioTdp>();
            StringBuilder error = new StringBuilder();

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
                            //error.Append(string.Format(Constantes.MENSAJE_ERROR_CAMPO_FORMATO,
                            //    Constantes.NOMBRE_CABECERA_FECHA_INICIO_INCENTIVO));
                        }

                        if (!DateTime.TryParse(cabeceras[Constantes.INDICE_CABECERA_FECHA_FIN_INCENTIVO],
                            out cabFechaFinIncentivo))
                        {
                            //error.Append(string.Format(Constantes.MENSAJE_ERROR_CAMPO_FORMATO,
                            //    Constantes.NOMBRE_CABECERA_FECHA_FIN_INCENTIVO));
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

            if (error.Length > 0) 
            {
                string rutaError = ConfigurationManager.AppSettings["rutaErrores"];
                if (Directory.Exists(rutaError))
                {
                    string archivoError = Path.Combine(rutaError, Path.GetFileNameWithoutExtension(rutaArchivo) + "_error.csv");
                    log.AppendLine("Hubo un error de validación en archivo y se encuentra en la siguiente dirección: " + archivoError);
                    log.AppendLine("");
                    File.WriteAllText(archivoError, error.ToString());
                }
                return null;
            }

            return listaPagoDiario;
        }

        private static void InsertarDatos(SqlTransaction trx, SqlConnection cnn, 
            DateTime fecha, List<PagoDiarioTdp> lista, StringBuilder log) 
        {
            PagoDiarioTdp pagoDiarioTdp = lista.FirstOrDefault();

            var procesoPagoDiario = new ProcesoPagoDiario();
            procesoPagoDiario.IncentivoId = pagoDiarioTdp.NumeroIncentivoTdp;
            procesoPagoDiario.Fecha = fecha;
            procesoPagoDiario.FechaHoraCreacion = DateTime.Now;


            using (SqlCommand cmd = new SqlCommand()) 
            {
                cmd.Transaction = trx;
                cmd.Connection = cnn;
                cmd.CommandText = "insert into ProcesoPagaDiario(Fecha, FechaHoraCreacion, IncentivoId) values (@Fecha, @FechaHoraCreacion, @IncentivoId); select SCOPE_IDENTITY();";
                cmd.Parameters.Add("@Fecha", SqlDbType.DateTime).Value = procesoPagoDiario.Fecha;
                cmd.Parameters.Add("@FechaHoraCreacion", SqlDbType.DateTime).Value = procesoPagoDiario.FechaHoraCreacion;
                cmd.Parameters.Add("@IncentivoId", SqlDbType.Int).Value = procesoPagoDiario.IncentivoId;

                procesoPagoDiario.ProcesoPagoDiarioId = Convert.ToInt32(cmd.ExecuteScalar());
            }
            lista.ForEach(x => x.ProcesoPagoDiarioId = procesoPagoDiario.ProcesoPagoDiarioId);
            var tabla = ConvertirADataTable(lista);

            using (SqlBulkCopy sbc = new SqlBulkCopy(cnn, SqlBulkCopyOptions.Default, trx))
            {
                sbc.BulkCopyTimeout = 200000;
                sbc.DestinationTableName = "PagaDiarioTDP";
                sbc.WriteToServer(tabla);

                log.AppendLine("Registros insertados a tabla PagaDiarioTDP: " + tabla.Rows.Count);
            }
        }

        private static bool LlamarUspValidarPagoDiario(SqlTransaction trx, SqlConnection cnn,
            DateTime fechaProceso, string rutaArchivo, StringBuilder log) 
        {
            DataSet datasetValidaciones = new DataSet();
            var tablaValidaciones = new DataTable();
            int numeroErrores = 0;

            using (SqlCommand cmd = new SqlCommand("uspValidarPagaDiario"))
            {
                cmd.Transaction = trx;
                cmd.Connection = cnn;
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 200000;

                SqlParameter par1 = cmd.Parameters.Add("@FechaProceso", SqlDbType.DateTime);
                par1.Direction = ParameterDirection.Input;
                par1.Value = fechaProceso;

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    adapter.Fill(datasetValidaciones);
                }


                if (datasetValidaciones != null && datasetValidaciones.Tables.Count > 0)
                {
                    for (int i = 0; i < datasetValidaciones.Tables.Count; i++)
                    {
                        tablaValidaciones = datasetValidaciones.Tables[i];

                        if (tablaValidaciones != null && tablaValidaciones.Rows.Count > 0)
                        {
                            numeroErrores += tablaValidaciones.Rows.Count;
                            string rutaError = ConfigurationManager.AppSettings["rutaErrores"];
                            if (Directory.Exists(rutaError))
                            {
                                string archivoError = Path.Combine(rutaError, Path.GetFileNameWithoutExtension(rutaArchivo) + "_error.csv");
                                log.AppendLine("Hubo un error de validación en archivo y se encuentra en la siguiente dirección: " + archivoError);
                                log.AppendLine("");
                                TablaATexto(tablaValidaciones, archivoError, ',');
                            }
                        }                        
                    }
                }

            }

            if (numeroErrores == 0)
                return true;
            else
                return false;

        }

        private static beIncentivoPDVGanadorLista LlamarUspCargarPagaDiario(DateTime fecha, StringBuilder log)
        {
            string cadenaConexion = ConfigurationManager.ConnectionStrings["con"].ConnectionString;

            using (SqlConnection con = new SqlConnection(cadenaConexion))
            {
                beIncentivoPDVGanadorLista obeIncentivoPDVGanadorLista = new beIncentivoPDVGanadorLista();
                SqlCommand cmd = new SqlCommand("uspCargarPagaDiario", con);
                cmd.CommandType = CommandType.StoredProcedure;

                SqlParameter par1 = cmd.Parameters.Add("@FechaProceso", SqlDbType.DateTime);
                par1.Direction = ParameterDirection.Input;
                par1.Value = fecha;

                con.Open();
                SqlDataReader drd = cmd.ExecuteReader();
                beIncentivoGanador obeIncentivoGanador = null;
                List<beIncentivoPDVGanador> lbeIncentivoPDVGanador = null;

                if (drd != null)
                {
                    if (drd.HasRows)
                    {
                        drd.Read();
                        int posIncentivoId = drd.GetOrdinal("IncentivoId");
                        int posDescripcion = drd.GetOrdinal("Descripcion");
                        int posFechaInicio = drd.GetOrdinal("FechaInicio");
                        int posFechaFin = drd.GetOrdinal("FechaFin");
                        obeIncentivoGanador = new beIncentivoGanador();
                        obeIncentivoGanador.IncentivoId = drd.GetInt32(posIncentivoId);
                        obeIncentivoGanador.Descripcion = drd.GetString(posDescripcion);
                        obeIncentivoGanador.FechaInicio = drd.GetDateTime(posFechaInicio);
                        obeIncentivoGanador.FechaFin = drd.GetDateTime(posFechaFin);
                        if (drd.NextResult())
                        {
                            int posIncentId = drd.GetOrdinal("IncentivoId");
                            int posDistribuidorId = drd.GetOrdinal("DistribuidorId");
                            int posPuntoVentaId = drd.GetOrdinal("PuntoVentaId");
                            int posNumeroCelular = drd.GetOrdinal("NumeroCelular");
                            int posMontoIncentivo = drd.GetOrdinal("MontoIncentivo");
                            lbeIncentivoPDVGanador = new List<beIncentivoPDVGanador>();
                            beIncentivoPDVGanador obeIncentivoPDVGanador;
                            while (drd.Read())
                            {
                                obeIncentivoPDVGanador = new beIncentivoPDVGanador();
                                obeIncentivoPDVGanador.IncentivoId = drd.GetInt32(posIncentId);
                                obeIncentivoPDVGanador.DistribuidorId = drd.GetString(posDistribuidorId);
                                obeIncentivoPDVGanador.PuntoVentaId = drd.GetString(posPuntoVentaId);
                                obeIncentivoPDVGanador.NumeroCelular = drd.GetString(posNumeroCelular);
                                obeIncentivoPDVGanador.MontoIncentivo = drd.GetDecimal(posMontoIncentivo);
                                lbeIncentivoPDVGanador.Add(obeIncentivoPDVGanador);
                            }
                        }
                    }
                    drd.Close();
                    obeIncentivoPDVGanadorLista.FechaCorrelativo = fecha.ToString("yyyyMMdd");
                }
                obeIncentivoPDVGanadorLista.IncentivoGanador = obeIncentivoGanador;
                obeIncentivoPDVGanadorLista.ListaIncentivoGanador = lbeIncentivoPDVGanador;
                log.AppendLine("Llamada a uspCargarPagaDiario, registros obtenidos: "+ lbeIncentivoPDVGanador.Count); 
                return (obeIncentivoPDVGanadorLista);
            }


        }

        private static void GenerarArchivoGanadores(beIncentivoPDVGanadorLista incentivoGanadorLista, StringBuilder log) 
        {
            if (incentivoGanadorLista == null || incentivoGanadorLista.IncentivoGanador == null || 
                incentivoGanadorLista.ListaIncentivoGanador.Count == 0)
                return;

			int x = 0;
            string rutaSalida = ConfigurationManager.AppSettings["rutaSalida"];
			beIncentivoPDVGanador obeIncentivoPDVGanador;
            string idDistribuidor = incentivoGanadorLista.ListaIncentivoGanador[0].DistribuidorId.Substring(0, 3);
			StringBuilder sb = new StringBuilder();
            beIncentivoGanador obeIncentivoGanador = incentivoGanadorLista.IncentivoGanador;
			sb.AppendLine(String.Format("{0},{1},{2},{3},{4}", idDistribuidor, obeIncentivoGanador.IncentivoId, obeIncentivoGanador.Descripcion, obeIncentivoGanador.FechaInicio, obeIncentivoGanador.FechaFin));
			string nombre;
			string archivo;
			string nombreZip;
			string archivoZip;
            for (int i = 0; i < incentivoGanadorLista.ListaIncentivoGanador.Count; i++)
			{
                obeIncentivoPDVGanador = incentivoGanadorLista.ListaIncentivoGanador[i];
				if (obeIncentivoPDVGanador.DistribuidorId.Substring(0, 3).Equals(idDistribuidor))
				{
					sb.AppendLine(String.Format("{0},{1},{2},{3},{4:n2}", obeIncentivoPDVGanador.IncentivoId, obeIncentivoPDVGanador.DistribuidorId, obeIncentivoPDVGanador.PuntoVentaId, obeIncentivoPDVGanador.NumeroCelular, obeIncentivoPDVGanador.MontoIncentivo));
				}
				//else
                if (!obeIncentivoPDVGanador.DistribuidorId.Substring(0, 3).Equals(idDistribuidor) || (i == incentivoGanadorLista.ListaIncentivoGanador.Count - 1))
				{
                    nombre = String.Format("G{0}_{1}_{2}.csv", incentivoGanadorLista.FechaCorrelativo, obeIncentivoGanador.IncentivoId, idDistribuidor);
					archivo = Path.Combine(rutaSalida, nombre);
					File.WriteAllText(archivo, sb.ToString());
                    log.AppendLine("Archivo generado " + archivo);
					idDistribuidor = obeIncentivoPDVGanador.DistribuidorId.Substring(0, 3);
					sb.Clear();
					sb.AppendLine(String.Format("{0},{1},{2},{3},{4}", idDistribuidor, obeIncentivoGanador.IncentivoId, obeIncentivoGanador.Descripcion, obeIncentivoGanador.FechaInicio, obeIncentivoGanador.FechaFin));
					sb.AppendLine(String.Format("{0},{1},{2},{3},{4:n2}", obeIncentivoPDVGanador.IncentivoId, obeIncentivoPDVGanador.DistribuidorId, obeIncentivoPDVGanador.PuntoVentaId, obeIncentivoPDVGanador.NumeroCelular, obeIncentivoPDVGanador.MontoIncentivo));
					x++;
				}
			}
        }

        private static void NotificarPorCorreo(string titulo, string contenido) 
        {
            beMensaje obeMensaje = new beMensaje();
            string[] correos = ConfigurationManager.AppSettings["CorreoCargaMasiva"].ToString().Split(';');
            obeMensaje.De = ConfigurationManager.AppSettings["CorreoDe"].ToString();
            obeMensaje.Clave = ConfigurationManager.AppSettings["CorreoClave"].ToString();
            obeMensaje.Para = correos;
            obeMensaje.Asunto = titulo;
            obeMensaje.Contenido = "<br>" + contenido.Replace("\n", "</br><br>") + "</br>";
            Console.WriteLine("Enviando Email");
            ucCorreo.enviar(obeMensaje);

        }
        
        private static DataTable ConvertirADataTable<T>(IList<T> data)
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

        private static void TablaATexto(DataTable tabla, string archivo, char separador)
        {
            using (FileStream fs = new FileStream(archivo, FileMode.Append, FileAccess.Write, FileShare.Write))
            {
                using (StreamWriter sw = new StreamWriter(fs, Encoding.Default))
                {
                    for (int i = 0; i < tabla.Columns.Count; i++)
                    {
                        sw.Write((char)34);
                        sw.Write(tabla.Columns[i].ColumnName);
                        sw.Write((char)34);
                        if (i < tabla.Columns.Count - 1) sw.Write(separador);
                    }
                    sw.WriteLine();
                    for (int j = 0; j < tabla.Rows.Count; j++)
                    {
                        for (int i = 0; i < tabla.Columns.Count; i++)
                        {
                            sw.Write((char)34);
                            sw.Write(tabla.Rows[j][i].ToString());
                            sw.Write((char)34);
                            if (i < tabla.Columns.Count - 1) sw.Write(separador);
                        }
                        sw.WriteLine();
                    }
                }
            }
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

    public class beIncentivoGanador
    {
        public int IncentivoId { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
    }

    public class beIncentivoPDVGanador
    {
        public int IncentivoId { get; set; }
        public string DistribuidorId { get; set; }
        public string PuntoVentaId { get; set; }
        public string NumeroCelular { get; set; }
        public decimal MontoIncentivo { get; set; }
    }

    public class beIncentivoPDVGanadorLista
    {
        public beIncentivoGanador IncentivoGanador { get; set; }
        public List<beIncentivoPDVGanador> ListaIncentivoGanador { get; set; }
        public string FechaCorrelativo { get; set; }
    }

}
