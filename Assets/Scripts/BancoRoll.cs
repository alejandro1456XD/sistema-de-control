using System;
using System.Globalization;
using UnityEngine;

// Banco de pruebas de 1 grado de libertad (roll).
// Se agrega al objeto "Dron". Fija su posición y deja libre solo el giro en roll,
// y cierra el lazo en cascada: ángulo (P) -> velocidad angular (PI) -> motores.
// Convención: roll positivo = lado derecho abajo.
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Rigidbody))]
public class BancoRoll : MonoBehaviour
{
    [Header("Parámetros de Python (opcional)")]
    public TextAsset parametrosJson;

    [Header("Ganancias (rad y rad/s)")]
    public float kpAngulo = 10f;
    public float kpVelocidad = 0.03f;
    public float kiVelocidad = 0.05f;
    public float kdVelocidad = 0f;

    [Header("Referencia")]
    public float referenciaGrados = 0f;
    public float pasoGrados = 15f;
    public bool filtrarReferencia = false;   // simula el filtro del mando gestual
    public float tauMando = 0.05f;

    [Header("Motores")]
    public float empujeMaxPorMotor = 0.3f;   // N por motor con comando = 1
    public float tauMotor = 0.03f;
    public float throttleBase = 0.49f;

    [Header("Perturbaciones")]
    public float impulsoTorque = 5e-5f;      // N·m·s al presionar Espacio
    public float torqueConstante = 1e-4f;    // N·m mientras D esté activo

    [Header("Simulación")]
    public float dtFisica = 0.002f;          // 500 Hz, igual que el lazo del ESP32
    public float tauFiltro = 0.3f;

    // Estado visible para el HUD
    float anguloEst, anguloReal, omega, omegaRef, error, u, pwmIzq, pwmDer;
    float torqueExterno;

    Rigidbody rb;
    IMUSimulator imu;
    TelemetryLogger logger;
    ComplementaryFilter filtro;
    PIDController pidAngulo;
    PIDController pidVelocidad;
    MotorModel[] motores = new MotorModel[4];

    float refFiltrada;
    float t0;
    bool impulsoPendiente;
    bool perturbacionActiva;
    GUIStyle estilo;

    static readonly string[] sufijos = { "1_FL", "2_FR", "3_BR", "4_BL" };
    static readonly bool[] esIzquierdo = { true, false, false, true };
    static readonly int[] reaccionYaw = { -1, 1, -1, 1 };

    // Clases para leer params.json (generado por analisis_dron.py)
    [Serializable] class PlantaJson { public float tau_m; }
    [Serializable] class PidJson { public float Kp; public float Ki; public float Kd; }
    [Serializable] class AnguloJson { public float Kp; }
    [Serializable] class GananciasJson { public PidJson rate; public AnguloJson angle; }
    [Serializable] class ParamsJson { public PlantaJson planta; public GananciasJson ganancias; public float control_dt; }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        CargarParametros();

        Time.fixedDeltaTime = dtFisica;
        rb.maxAngularVelocity = 50f;   // el valor por defecto (7 rad/s) frenaría al dron
        rb.sleepThreshold = 0f;
        rb.constraints = RigidbodyConstraints.FreezePosition
                       | RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationY;

        imu = GetOrAdd<IMUSimulator>(gameObject);
        logger = GetOrAdd<TelemetryLogger>(gameObject);
        CrearMotores();

        filtro = new ComplementaryFilter { tau = tauFiltro };
        pidAngulo = new PIDController(kpAngulo, 0f, 0f, -8.7f, 8.7f);          // ±500 °/s
        pidVelocidad = new PIDController(kpVelocidad, kiVelocidad, kdVelocidad, -0.5f, 0.5f);
        pidVelocidad.integralLimit = 0.3f;
    }

    void Start()
    {
        t0 = Time.fixedTime;
        logger.Begin(new[]
        {
            "t", "ref_deg", "theta_est_deg", "theta_real_deg", "omega_deg_s",
            "omega_ref_deg_s", "error_deg", "u", "pwm_izq", "pwm_der", "torque_ext"
        });
    }

    void CargarParametros()
    {
        if (parametrosJson == null) return;
        try
        {
            var p = JsonUtility.FromJson<ParamsJson>(parametrosJson.text);
            if (p.ganancias != null)
            {
                if (p.ganancias.angle != null) kpAngulo = p.ganancias.angle.Kp;
                if (p.ganancias.rate != null)
                {
                    kpVelocidad = p.ganancias.rate.Kp;
                    kiVelocidad = p.ganancias.rate.Ki;
                    kdVelocidad = p.ganancias.rate.Kd;
                }
            }
            if (p.planta != null && p.planta.tau_m > 0f) tauMotor = p.planta.tau_m;
            if (p.control_dt > 0f) dtFisica = p.control_dt;
            Debug.Log("BancoRoll: parámetros cargados de " + parametrosJson.name +
                      " (Kp ángulo " + kpAngulo + ", Kp vel " + kpVelocidad + ", Ki vel " + kiVelocidad + ")");
        }
        catch (Exception e)
        {
            Debug.LogWarning("BancoRoll: no se pudo leer el JSON, uso los valores del Inspector. " + e.Message);
        }
    }

    void CrearMotores()
    {
        for (int i = 0; i < 4; i++)
        {
            Transform punto = BuscarHijo("Empuje_" + sufijos[i]);
            Transform helice = BuscarHijo("Prop_" + sufijos[i]);
            if (punto == null)
            {
                Debug.LogError("BancoRoll: no encuentro el objeto Empuje_" + sufijos[i]);
                enabled = false;
                return;
            }
            var m = GetOrAdd<MotorModel>(punto.gameObject);
            m.Setup(rb, helice, empujeMaxPorMotor, tauMotor, reaccionYaw[i]);
            motores[i] = m;
        }
    }

    Transform BuscarHijo(string nombre)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;
        return null;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    void Update()
    {
        if (EntradaTeclado.Presionada(Tecla.Izquierda)) referenciaGrados -= 5f;
        if (EntradaTeclado.Presionada(Tecla.Derecha)) referenciaGrados += 5f;
        if (EntradaTeclado.Presionada(Tecla.Cero)) referenciaGrados = 0f;
        if (EntradaTeclado.Presionada(Tecla.Uno)) referenciaGrados = pasoGrados;
        if (EntradaTeclado.Presionada(Tecla.Dos)) referenciaGrados = -pasoGrados;
        if (EntradaTeclado.Presionada(Tecla.Espacio)) impulsoPendiente = true;
        if (EntradaTeclado.Presionada(Tecla.Perturbacion)) perturbacionActiva = !perturbacionActiva;
        if (EntradaTeclado.Presionada(Tecla.Reiniciar)) Reiniciar();
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // Perturbaciones: un empujón puntual y/o un par constante (hacia la derecha)
        torqueExterno = 0f;
        if (impulsoPendiente)
        {
            rb.AddTorque(0f, 0f, -impulsoTorque, ForceMode.Impulse);
            impulsoPendiente = false;
        }
        if (perturbacionActiva)
        {
            rb.AddTorque(0f, 0f, -torqueConstante);
            torqueExterno = torqueConstante;
        }

        // Sensor y estimación
        imu.Sample(dt);
        filtro.Update(imu.Gyro, imu.Accel, dt);

        // Referencia (con filtro opcional del mando)
        float refRad = referenciaGrados * Mathf.Deg2Rad;
        if (filtrarReferencia)
        {
            refFiltrada += (refRad - refFiltrada) * (dt / (tauMando + dt));
            refRad = refFiltrada;
        }
        else
        {
            refFiltrada = refRad;
        }

        // Lazo externo (ángulo) y lazo interno (velocidad)
        float theta = filtro.Roll;
        omega = -imu.Gyro.z;
        float errorAngulo = refRad - theta;
        float omegaRefRad = pidAngulo.Update(errorAngulo, dt);
        u = pidVelocidad.Update(omegaRefRad - omega, dt);

        // Mixer de roll: más empuje a la izquierda baja el lado derecho
        pwmIzq = Mathf.Clamp01(throttleBase + u);
        pwmDer = Mathf.Clamp01(throttleBase - u);
        for (int i = 0; i < 4; i++)
            motores[i].Command = esIzquierdo[i] ? pwmIzq : pwmDer;

        // Datos para HUD y registro
        anguloEst = theta * Mathf.Rad2Deg;
        anguloReal = -Mathf.DeltaAngle(0f, transform.eulerAngles.z);
        omegaRef = omegaRefRad * Mathf.Rad2Deg;
        error = errorAngulo * Mathf.Rad2Deg;
        float omegaDeg = omega * Mathf.Rad2Deg;

        logger.Row(Time.fixedTime - t0, referenciaGrados, anguloEst, anguloReal, omegaDeg,
                   omegaRef, error, u, pwmIzq, pwmDer, torqueExterno);
    }

    void Reiniciar()
    {
        rb.angularVelocity = Vector3.zero;
        rb.rotation = Quaternion.identity;
        transform.rotation = Quaternion.identity;
        filtro.Reset();
        pidAngulo.Reset();
        pidVelocidad.Reset();
        imu.ResetSensor();
        foreach (var m in motores) m.ResetMotor();
        referenciaGrados = 0f;
        refFiltrada = 0f;
        perturbacionActiva = false;
    }

    void OnGUI()
    {
        if (estilo == null)
        {
            estilo = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            estilo.normal.textColor = Color.white;
        }
        string txt =
            "BANCO 1-GDL (roll)\n" +
            "Referencia: " + referenciaGrados.ToString("F1", CultureInfo.InvariantCulture) + " °\n" +
            "Ángulo estimado: " + anguloEst.ToString("F1", CultureInfo.InvariantCulture) + " °\n" +
            "Ángulo real: " + anguloReal.ToString("F1", CultureInfo.InvariantCulture) + " °\n" +
            "Error: " + error.ToString("F2", CultureInfo.InvariantCulture) + " °\n" +
            "u: " + u.ToString("F3", CultureInfo.InvariantCulture) +
            "   PWM izq/der: " + pwmIzq.ToString("F2", CultureInfo.InvariantCulture) +
            " / " + pwmDer.ToString("F2", CultureInfo.InvariantCulture) + "\n" +
            "Perturbación constante: " + (perturbacionActiva ? "ACTIVA" : "apagada") + "\n\n" +
            "0: 0°   1: +" + pasoGrados + "°   2: -" + pasoGrados + "°\n" +
            "← →: ±5°   Espacio: empujón   D: par constante   R: reiniciar";
        GUI.Label(new Rect(12, 10, 640, 260), txt, estilo);
    }
}
