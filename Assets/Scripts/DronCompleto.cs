using System;
using System.Globalization;
using UnityEngine;

// Dron completo: 4 motores, mixer X y tres lazos de control.
//   Roll y pitch: ángulo (P) -> velocidad angular (PI)
//   Yaw: solo velocidad angular (PI), no hay magnetómetro
//   Altura: lazo abierto, el throttle se pasa directo al mixer
// Se agrega al objeto "Dron" (reemplaza a BancoRoll).
// Convención: roll positivo = lado derecho abajo, pitch positivo = nariz arriba.
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Rigidbody))]
public class DronCompleto : MonoBehaviour
{
    [Header("Parámetros de Python (opcional)")]
    public TextAsset parametrosJson;

    [Header("Ganancias roll / pitch (rad y rad/s)")]
    public float kpAngulo = 10f;
    public float kpVelocidad = 0.03f;
    public float kiVelocidad = 0.05f;

    [Header("Ganancias yaw (rad/s)")]
    public float kpYaw = 0.3f;
    public float kiYaw = 0.5f;

    [Header("Referencias")]
    public float rollGrados;
    public float pitchGrados;
    public float yawVelGradosS;
    public float throttle = 0f;                    // en el suelo: motores apagados
    public const float hoverThrottle = 0.4905f;   // m·g / (4 · empuje máximo)
    public float pasoGrados = 15f;
    public float pasoYawGradosS = 45f;

    [Header("Motores")]
    public float empujeMaxPorMotor = 0.3f;
    public float tauMotor = 0.03f;

    [Header("Perturbaciones")]
    public float impulsoTorque = 5e-5f;
    public Vector3 torqueConstante = new Vector3(0f, 0f, -1e-4f);   // N·m en ejes de Unity
    public bool perturbacionActiva;

    [Header("Simulación")]
    public float dtFisica = 0.002f;
    public float tauFiltro = 10f;            // largo: en vuelo el acelerómetro no mide la gravedad
    public bool compensarInclinacion = true; // sube el throttle al inclinarse para no perder altura
    public float arrastreLineal = 1.5f;      // 1/s, arrastre aerodinámico
    public int muestrasCalibracion = 250;    // 0.5 s en hover para medir el sesgo del giroscopio
    public bool congelarPosicion;

    // Estado visible
    public float RollEst { get; private set; }
    public float PitchEst { get; private set; }
    public float RollReal { get; private set; }
    public float PitchReal { get; private set; }
    public float YawVelReal { get; private set; }

    Rigidbody rb;
    IMUSimulator imu;
    TelemetryLogger logger;
    ComplementaryFilter filtro;
    PIDController pidAnguloRoll, pidVelRoll, pidAnguloPitch, pidVelPitch, pidYaw;
    MotorModel[] motores = new MotorModel[4];
    float uRoll, uPitch, uYaw;
    float t0;
    bool impulsoPendiente;
    bool calibrando = true;
    int muestras;
    Vector3 sumaGiro, sesgoGiro;
    Vector3 posInicial;
    GUIStyle estilo;

    // Orden de motores: 1 FL, 2 FR, 3 BR, 4 BL
    static readonly string[] sufijos = { "1_FL", "2_FR", "3_BR", "4_BL" };
    static readonly int[] izquierda = { 1, -1, -1, 1 };      // +1 si es motor izquierdo
    static readonly int[] delante = { 1, 1, -1, -1 };        // +1 si es motor delantero
    static readonly int[] reaccionYaw = { -1, 1, -1, 1 };

    [Serializable] class PlantaJson { public float tau_m; }
    [Serializable] class PidJson { public float Kp; public float Ki; public float Kd; }
    [Serializable] class AnguloJson { public float Kp; }
    [Serializable] class YawJson { public float Kp; public float Ki; }
    [Serializable] class GananciasJson { public PidJson rate; public AnguloJson angle; public YawJson yaw; }
    [Serializable] class ParamsJson { public PlantaJson planta; public GananciasJson ganancias; public float control_dt; }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        CargarParametros();

        Time.fixedDeltaTime = dtFisica;
        rb.maxAngularVelocity = 50f;
        rb.sleepThreshold = 0f;
        rb.linearDamping = arrastreLineal;
        rb.constraints = congelarPosicion ? RigidbodyConstraints.FreezePosition : RigidbodyConstraints.None;
        posInicial = transform.position;

        imu = GetOrAdd<IMUSimulator>(gameObject);
        logger = GetOrAdd<TelemetryLogger>(gameObject);
        logger.prefijo = "dron";
        CrearMotores();

        filtro = new ComplementaryFilter { tau = tauFiltro };
        pidAnguloRoll = new PIDController(kpAngulo, 0f, 0f, -8.7f, 8.7f);
        pidAnguloPitch = new PIDController(kpAngulo, 0f, 0f, -8.7f, 8.7f);
        pidVelRoll = CrearPidVelocidad(kpVelocidad, kiVelocidad);
        pidVelPitch = CrearPidVelocidad(kpVelocidad, kiVelocidad);
        pidYaw = CrearPidVelocidad(kpYaw, kiYaw);
    }

    static PIDController CrearPidVelocidad(float kp, float ki)
    {
        var pid = new PIDController(kp, ki, 0f, -0.5f, 0.5f);
        pid.integralLimit = 0.3f;
        return pid;
    }

    void Start()
    {
        foreach (var m in motores) m.Preset(throttle);
        t0 = Time.fixedTime;
        logger.Begin(new[]
        {
            "t", "roll_ref_deg", "roll_est_deg", "roll_real_deg",
            "pitch_ref_deg", "pitch_est_deg", "pitch_real_deg",
            "yaw_ref_deg_s", "yaw_real_deg_s", "alt_m",
            "u_roll", "u_pitch", "u_yaw", "pwm1", "pwm2", "pwm3", "pwm4"
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
                }
                if (p.ganancias.yaw != null)
                {
                    kpYaw = p.ganancias.yaw.Kp;
                    kiYaw = p.ganancias.yaw.Ki;
                }
            }
            if (p.planta != null && p.planta.tau_m > 0f) tauMotor = p.planta.tau_m;
            if (p.control_dt > 0f) dtFisica = p.control_dt;
        }
        catch (Exception e)
        {
            Debug.LogWarning("DronCompleto: no se pudo leer el JSON, uso los valores del Inspector. " + e.Message);
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
                Debug.LogError("DronCompleto: no encuentro el objeto Empuje_" + sufijos[i]);
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
        if (EntradaTeclado.Presionada(Tecla.Izquierda)) rollGrados -= 5f;
        if (EntradaTeclado.Presionada(Tecla.Derecha)) rollGrados += 5f;
        if (EntradaTeclado.Presionada(Tecla.Arriba)) pitchGrados += 5f;
        if (EntradaTeclado.Presionada(Tecla.Abajo)) pitchGrados -= 5f;
        if (EntradaTeclado.Presionada(Tecla.YawIzq)) yawVelGradosS -= pasoYawGradosS;
        if (EntradaTeclado.Presionada(Tecla.YawDer)) yawVelGradosS += pasoYawGradosS;
        if (EntradaTeclado.Presionada(Tecla.GasMas)) throttle = Mathf.Clamp01(throttle + 0.02f);
        if (EntradaTeclado.Presionada(Tecla.GasMenos)) throttle = Mathf.Clamp01(throttle - 0.02f);
        if (EntradaTeclado.Presionada(Tecla.Cero)) { rollGrados = 0f; pitchGrados = 0f; yawVelGradosS = 0f; }
        if (EntradaTeclado.Presionada(Tecla.Uno)) rollGrados = pasoGrados;
        if (EntradaTeclado.Presionada(Tecla.Dos)) pitchGrados = pasoGrados;
        if (EntradaTeclado.Presionada(Tecla.Espacio)) impulsoPendiente = true;
        if (EntradaTeclado.Presionada(Tecla.Perturbacion)) perturbacionActiva = !perturbacionActiva;
        if (EntradaTeclado.Presionada(Tecla.Congelar)) FijarPosicion(!congelarPosicion);
        if (EntradaTeclado.Presionada(Tecla.Reiniciar)) Reiniciar();
    }

    public void FijarPosicion(bool fijar)
    {
        congelarPosicion = fijar;
        rb.constraints = fijar ? RigidbodyConstraints.FreezePosition : RigidbodyConstraints.None;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (impulsoPendiente)
        {
            rb.AddTorque(0f, 0f, -impulsoTorque, ForceMode.Impulse);
            impulsoPendiente = false;
        }
        if (perturbacionActiva) rb.AddTorque(torqueConstante);

        // Sensor y estimación
        imu.Sample(dt);
        if (calibrando)
        {
            // Al armar: el dron queda en hover sin control y se mide el sesgo del giroscopio
            sumaGiro += imu.Gyro;
            if (++muestras >= muestrasCalibracion)
            {
                sesgoGiro = sumaGiro / muestras;
                calibrando = false;
                filtro.Reset();
            }
            return;
        }

        Vector3 gyro = imu.Gyro - sesgoGiro;
        filtro.Update(gyro, imu.Accel, dt);
        RollEst = filtro.Roll;
        PitchEst = filtro.Pitch;

        float wRoll = -gyro.z;
        float wPitch = -gyro.x;
        float wYaw = gyro.y;

        // Roll y pitch: P de ángulo -> PI de velocidad
        float wRefRoll = pidAnguloRoll.Update(rollGrados * Mathf.Deg2Rad - RollEst, dt);
        float wRefPitch = pidAnguloPitch.Update(pitchGrados * Mathf.Deg2Rad - PitchEst, dt);
        uRoll = pidVelRoll.Update(wRefRoll - wRoll, dt);
        uPitch = pidVelPitch.Update(wRefPitch - wPitch, dt);

        // Yaw: solo velocidad
        uYaw = pidYaw.Update(yawVelGradosS * Mathf.Deg2Rad - wYaw, dt);

        // Al inclinarse, la componente vertical del empuje baja con cos(roll)·cos(pitch)
        float gas = throttle;
        if (compensarInclinacion)
            gas /= Mathf.Max(0.7f, Mathf.Cos(RollEst) * Mathf.Cos(PitchEst));

        // En el suelo, sin throttle de despegue: motores apagados y lazos reiniciados
        bool enSuelo = transform.position.y < posInicial.y + 0.03f;
        if (enSuelo && throttle <= hoverThrottle + 0.002f)
        {
            foreach (var m in motores) m.Command = 0f;
            pidAnguloRoll.Reset();
            pidAnguloPitch.Reset();
            pidVelRoll.Reset();
            pidVelPitch.Reset();
            pidYaw.Reset();
        }
        else
        {
            // Mixer X
            for (int i = 0; i < 4; i++)
            {
                float pwm = gas + izquierda[i] * uRoll + delante[i] * uPitch + reaccionYaw[i] * uYaw;
                motores[i].Command = Mathf.Clamp01(pwm);
            }
        }

        // Datos reales (para HUD y registro)
        RollReal = -Mathf.Asin(Mathf.Clamp(transform.right.y, -1f, 1f)) * Mathf.Rad2Deg;
        PitchReal = Mathf.Asin(Mathf.Clamp(transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        YawVelReal = rb.angularVelocity.y * Mathf.Rad2Deg;

        logger.Row(Time.fixedTime - t0, rollGrados, RollEst * Mathf.Rad2Deg, RollReal,
                   pitchGrados, PitchEst * Mathf.Rad2Deg, PitchReal,
                   yawVelGradosS, YawVelReal, transform.position.y,
                   uRoll, uPitch, uYaw,
                   motores[0].Command, motores[1].Command, motores[2].Command, motores[3].Command);
    }

    public void Reiniciar()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = posInicial;
        rb.rotation = Quaternion.identity;
        transform.SetPositionAndRotation(posInicial, Quaternion.identity);
        filtro.Reset();
        pidAnguloRoll.Reset();
        pidAnguloPitch.Reset();
        pidVelRoll.Reset();
        pidVelPitch.Reset();
        pidYaw.Reset();
        imu.ResetSensor();
        calibrando = true;
        muestras = 0;
        sumaGiro = Vector3.zero;
        throttle = 0f;
        foreach (var m in motores) m.Preset(throttle);
        rollGrados = 0f;
        pitchGrados = 0f;
        yawVelGradosS = 0f;
        perturbacionActiva = false;
    }

    void OnGUI()
    {
        if (estilo == null)
        {
            estilo = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            estilo.normal.textColor = Color.white;
        }
        var ci = CultureInfo.InvariantCulture;
        string txt =
            "DRON COMPLETO\n" +
            "Roll   ref " + rollGrados.ToString("F1", ci) + "°   real " + RollReal.ToString("F1", ci) + "°\n" +
            "Pitch  ref " + pitchGrados.ToString("F1", ci) + "°   real " + PitchReal.ToString("F1", ci) + "°\n" +
            "Yaw    ref " + yawVelGradosS.ToString("F0", ci) + " °/s   real " + YawVelReal.ToString("F0", ci) + " °/s\n" +
            "Throttle " + throttle.ToString("F2", ci) + "   Altura " + transform.position.y.ToString("F2", ci) + " m\n" +
            "Perturbación constante: " + (perturbacionActiva ? "ACTIVA" : "apagada") +
            "   Posición fija: " + (congelarPosicion ? "sí" : "no") + "\n\n" +
            "Flechas: roll/pitch ±5°   1: roll 15°   2: pitch 15°   0: todo a cero\n" +
            "Q / E: yaw ∓45 °/s   W / S: throttle ±0.02\n" +
            "Espacio: empujón   G: par constante   F: fijar posición   R: reiniciar";
        GUI.Label(new Rect(12, 10, 760, 260), txt, estilo);
    }
}
