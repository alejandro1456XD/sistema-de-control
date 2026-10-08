using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Guarda columnas de datos en un CSV dentro de la carpeta "Telemetria" del proyecto.
// El archivo se escribe al salir de Play.
public class TelemetryLogger : MonoBehaviour
{
    public string prefijo = "banco";
    public int maxFilas = 200000;

    StringBuilder sb = new StringBuilder();
    int filas;
    bool guardado = true;

    public void Begin(string[] columnas)
    {
        sb.Clear();
        sb.AppendLine(string.Join(",", columnas));
        filas = 0;
        guardado = false;
    }

    public void Row(params float[] valores)
    {
        if (guardado || filas >= maxFilas) return;
        for (int i = 0; i < valores.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(valores[i].ToString("G7", CultureInfo.InvariantCulture));
        }
        sb.AppendLine();
        filas++;
    }

    public void Save()
    {
        if (guardado || filas == 0) return;
        guardado = true;

        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Telemetria"));
        Directory.CreateDirectory(dir);
        string nombre = prefijo + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        string ruta = Path.Combine(dir, nombre);
        File.WriteAllText(ruta, sb.ToString());
        Debug.Log("Telemetría guardada (" + filas + " filas): " + ruta);
    }

    void OnDisable()
    {
        Save();
    }
}
