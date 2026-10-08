using UnityEngine;

// Motor + hélice: el empuje sigue al comando con un retardo de primer orden (tau)
// y se aplica como fuerza en el punto del motor. Lo configura el script de control.
public class MotorModel : MonoBehaviour
{
    public float maxThrust = 0.3f;        // N con comando = 1
    public float tau = 0.03f;             // constante de tiempo del motor [s]
    public float yawCoeff = 0.00375f;     // par de reacción por newton de empuje [m]
    public int yawReaction = 1;           // sentido del par de reacción sobre Y
    public Transform helice;              // pieza visual que gira (su eje es Z local)
    public int spinDir = 1;
    public float visualMaxSpeed = 1500f;  // grados/s a empuje máximo, solo estético

    public float Command { get; set; }    // 0..1
    public float Thrust { get; private set; }

    Rigidbody rb;

    public void Setup(Rigidbody body, Transform prop, float maxThrust, float tau, int yawReaction)
    {
        rb = body;
        helice = prop;
        this.maxThrust = maxThrust;
        this.tau = tau;
        this.yawReaction = yawReaction;
        spinDir = -yawReaction;
    }

    public void ResetMotor()
    {
        Command = 0f;
        Thrust = 0f;
    }

    // Deja el motor ya girando al comando dado (por ejemplo, el hover al arrancar)
    public void Preset(float command)
    {
        Command = command;
        Thrust = Mathf.Clamp01(command) * maxThrust;
    }

    void FixedUpdate()
    {
        if (rb == null) return;
        float dt = Time.fixedDeltaTime;

        float target = Mathf.Clamp01(Command) * maxThrust;
        Thrust += (target - Thrust) * (1f - Mathf.Exp(-dt / tau));

        rb.AddForceAtPosition(transform.up * Thrust, transform.position);
        rb.AddTorque(transform.up * (yawReaction * yawCoeff * Thrust));
    }

    void Update()
    {
        if (helice == null || maxThrust <= 0f) return;
        float fraccion = Mathf.Sqrt(Mathf.Clamp01(Thrust / maxThrust));
        helice.Rotate(0f, 0f, spinDir * visualMaxSpeed * fraccion * Time.deltaTime, Space.Self);
    }
}
