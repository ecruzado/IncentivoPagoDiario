using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace IncentivoPagoDiario
{
    public class ucCorreo
    {
        public static string fomatoAMD(string texto, string extension = "")
        {
            DateTime fechaActual = DateTime.Now;
            string formato = String.Format("{0}_{1}_{2}_{3}{4}", texto, fechaActual.Year,
                fechaActual.Month.ToString().PadLeft(2, '0'),
                fechaActual.Day.ToString().PadLeft(2, '0'),
                extension);
            return (formato);
        }

        public static bool enviar(beMensaje obeMensaje)
        {
            bool exito = false;
            string rutaLog = ConfigurationManager.AppSettings["rutaLog"];
            string archivo = String.Format("{0}{1}", rutaLog, fomatoAMD("LogError", ".txt"));
            try
            {
                string servidor = ConfigurationManager.AppSettings["CorreoServidor"];
                string puerto = ConfigurationManager.AppSettings["CorreoPuerto"];
                string usuario = ConfigurationManager.AppSettings["CorreoUsuario"];
                bool ssl = (ConfigurationManager.AppSettings["CorreoSSL"].ToLower() == "true" ? true : false);
                MailMessage eMail = new MailMessage();
                eMail.Subject = obeMensaje.Asunto;
                eMail.IsBodyHtml = true;
                eMail.Body = obeMensaje.Contenido;
                eMail.From = new MailAddress(obeMensaje.De);
                if (obeMensaje.Para != null && obeMensaje.Para.Length > 0)
                {
                    foreach (string para in obeMensaje.Para)
                    {
                        eMail.To.Add(new MailAddress(para));
                    }
                }
                if (obeMensaje.CC != null && obeMensaje.CC.Length > 0)
                {
                    foreach (string cc in obeMensaje.CC)
                    {
                        eMail.CC.Add(new MailAddress(cc));
                    }
                }
                SmtpClient smtp = new SmtpClient();
                smtp.Host = servidor;
                int n;
                bool res = int.TryParse(puerto, out n);
                if (!res) n = 25;
                smtp.Port = n;
                smtp.EnableSsl = ssl;
                smtp.UseDefaultCredentials = false;
                smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtp.Credentials = new NetworkCredential(usuario, obeMensaje.Clave);
                smtp.Send(eMail);
                exito = true;
            }
            catch (Exception ex)
            {
                ucObjeto<Exception>.grabarArchivoTexto(ex, archivo);
            }
            return (exito);
        }
    }
}
