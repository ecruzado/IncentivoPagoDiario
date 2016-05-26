using System;
using System.Collections.Generic; //T
using System.Reflection; //PropertyInfo
using System.IO; //StreamWriter
using System.Text; //Encoding

namespace IncentivoPagoDiario
{
    public class ucObjeto<T>
    {
        public static void grabarArchivoTexto(T obj, string archivo)
        {
            PropertyInfo[] propiedades = obj.GetType().GetProperties();
            using (FileStream fs = new FileStream(archivo, FileMode.Append, FileAccess.Write, FileShare.Write))
            {
                using (StreamWriter sw = new StreamWriter(fs, Encoding.Default))
                {
                    foreach (PropertyInfo propiedad in propiedades)
                    {
                        sw.Write(propiedad.Name);
                        sw.Write(" = ");
                        sw.WriteLine(propiedad.GetValue(obj, null) == null ? "" : propiedad.GetValue(obj, null).ToString());
                    }
                    sw.WriteLine(new String('_', 50));
                }
            }
        }
    }
}
