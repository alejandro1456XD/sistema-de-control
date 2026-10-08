using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// Recibe por UDP las referencias del mando gestual (cámara + MediaPipe en Python)
// y las pasa a DronCompleto. Mensaje de texto: "roll,pitch,yaw,throttle,ok"
//   roll, pitch en grados; yaw en °/s; throttle 0..1; ok = 1 si hay mano detectada.
// Si no llegan datos por más de timeoutS, el dron vuelve a hover (referencias en cero).
[DefaultExecutionOrder(-40)]
[RequireComponent(typeof(DronCompleto))]
public class GestoUDP : MonoBehaviour
{
    public int puerto = 5005;
    public float timeoutS = 0.3f;
    public bool activo = true;

    DronCompleto dron;
    UdpClient cliente;
    Thread hilo;
    volatile bool corriendo;
    readonly object cerrojo = new object();
    float roll, pitch, yaw, gas;
    bool manoOk;
    double ultimoMensaje = -1;
    int paquetes;

    public bool Conectado { get; private set; }

    void Awake()
    {
        dron = GetComponent<DronCompleto>();
    }

    void OnEnable()
    {
        try
        {
            cliente = new UdpClient(puerto);
            corriendo = true;
            hilo = new Thread(Escuchar) { IsBackground = true };
            hilo.Start();
        }
        catch (Exception e)
        {
            Debug.LogWarning("GestoUDP: no se pudo abrir el puerto " + puerto + ". " + e.Message);
        }
    }

    void OnDisable()
    {
        corriendo = false;
        if (cliente != null) cliente.Close();
        cliente = null;
    }

    void Escuchar()
    {
        var ep = new IPEndPoint(IPAddress.Any, 0);
        while (corriendo)
        {
            try
            {
                byte[] datos = cliente.Receive(ref ep);
                string[] p = Encoding.ASCII.GetString(datos).Trim().Split(',');
                if (p.Length < 5) continue;
                var ci = CultureInfo.InvariantCulture;
                float r = float.Parse(p[0], ci), pi = float.Parse(p[1], ci);
                float y = float.Parse(p[2], ci), g = float.Parse(p[3], ci);
                bool ok = p[4].Trim() == "1";
                lock (cerrojo)
                {
                    roll = r; pitch = pi; yaw = y; gas = g; manoOk = ok;
                    ultimoMensaje = AhoraS();
                    paquetes++;
                }
            }
            catch (SocketException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (FormatException) { }
        }
    }

    static double AhoraS()
    {
        return DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
    }

    void FixedUpdate()
    {
        float r, p, y, g;
        bool ok;
        double t;
        lock (cerrojo)
        {
            r = roll; p = pitch; y = yaw; g = gas; ok = manoOk; t = ultimoMensaje;
        }

        Conectado = t > 0 && AhoraS() - t < timeoutS;
        if (!activo || !Conectado) return;

        if (ok)
        {
            dron.rollGrados = r;
            dron.pitchGrados = p;
            dron.yawVelGradosS = y;
            dron.throttle = Mathf.Clamp01(g);
        }
        else
        {
            // Sin mano: referencias en cero y throttle de hover
            dron.rollGrados = 0f;
            dron.pitchGrados = 0f;
            dron.yawVelGradosS = 0f;
            dron.throttle = DronCompleto.hoverThrottle;
        }
    }
}
