using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IncentivoPagoDiario
{
    public class Constantes
    {
        public const char CARACTER_SEPARACION = ',';

        public const int NUMERO_CAMPOS_CABECERA = 5;
        public const int NUMERO_CAMPOS = 5;

        public const string NOMBRE_CABECERA_CODIGO_DISTRIBUIDOR = "Código de distribuidor TDP";
        public const string NOMBRE_CABECERA_IDENTIFICADOR_INCENTIVO = "Identificador de Incentivo TDP";
        public const string NOMBRE_CABECERA_DESCRIPCION_INCENTIVO = "Descripción de Incentivo TDP";
        public const string NOMBRE_CABECERA_FECHA_INICIO_INCENTIVO = "Fecha y hora de inicio de Incentivo";
        public const string NOMBRE_CABECERA_FECHA_FIN_INCENTIVO = "Fecha y hora de fin de Incentivo";

        public const int INDICE_CABECERA_CODIGO_DISTRIBUIDOR = 0;
        public const int INDICE_CABECERA_IDENTIFICADOR_INCENTIVO = 1;
        public const int INDICE_CABECERA_DESCRIPCION_INCENTIVO = 2;
        public const int INDICE_CABECERA_FECHA_INICIO_INCENTIVO = 3;
        public const int INDICE_CABECERA_FECHA_FIN_INCENTIVO = 4;

        public const string NOMBRE_CAMPO_IDENTIFICADOR_INCENTIVO = "Identificador de Incentivo TDP";
        public const string NOMBRE_CAMPO_CODIGO_DISTRIBUIDOR = "Código de distribuidor";
        public const string NOMBRE_CAMPO_CODIGO_PDV = "Código de PDV";
        public const string NOMBRE_CAMPO_CODIGO_CELULAR = "Código de Celular";
        public const string NOMBRE_CAMPO_MONTO = "Monto";

        public const int INDICE_CAMPO_IDENTIFICADOR_INCENTIVO = 0;
        public const int INDICE_CAMPO_CODIGO_DISTRIBUIDOR = 1;
        public const int INDICE_CAMPO_CODIGO_PDV = 2;
        public const int INDICE_CAMPO_CODIGO_CELULAR = 3;
        public const int INDICE_CAMPO_MONTO = 4;

        public const string MENSAJE_ERROR_NUMERO_CAMPOS_INCORRECTO = 
            "Fila {0} - El número de campos debe ser {1}.";

        public const string MENSAJE_ERROR_CAMPO_VACIO =
            "Campo {0} esta vacio.";

        public const string MENSAJE_ERROR_CAMPO_FORMATO =
            "Campo {0} no tiene formato adecuado.";
    }
}
