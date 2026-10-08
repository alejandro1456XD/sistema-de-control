using UnityEngine;

// Simula lo que mediría un MPU6050 pegado al dron: giroscopio y acelerómetro
// en ejes del cuerpo, con ruido y sesgo. El controlador solo ve estos valores.
[RequireComponent(typeof(Rigidbody))]
public class IMUSimulator : MonoBehaviour
{
    [Header("Ruido")]
    public float gyroNoise = 0.003f;    // rad/s (desviación estándar)
    public float gyroBias = 0.01f;      // rad/s (sesgo constante aleatorio)
    public float accelNoise = 0.05f;    // m/s²
    public int seed = 1234;

    public Vector3 Gyro { get; private set; }    // rad/s
    public Vector3 Accel { get; private set; }   // m/s²

    Rigidbody rb;
    System.Random rng;
    Vector3 bias;
    Vector3 lastVelocity;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rng = new System.Random(seed);
        bias = new Vector3(Gauss(), Gauss(), Gauss()) * gyroBias;
        lastVelocity = rb.linearVelocity;
    }

    public void ResetSensor()
    {
        lastVelocity = rb.linearVelocity;
    }

    // Se llama una vez por paso de física, antes de calcular el control
    public void Sample(float dt)
    {
        Quaternion inv = Quaternion.Inverse(rb.rotation);

        Vector3 wBody = inv * rb.angularVelocity;

        Vector3 aWorld = (rb.linearVelocity - lastVelocity) / dt;
        lastVelocity = rb.linearVelocity;
        Vector3 fBody = inv * (aWorld - Physics.gravity);

        Gyro = wBody + bias + NoiseVector(gyroNoise);
        Accel = fBody + NoiseVector(accelNoise);
    }

    Vector3 NoiseVector(float std)
    {
        return new Vector3(Gauss(), Gauss(), Gauss()) * std;
    }

    float Gauss()
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2));
    }
}
