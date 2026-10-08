using UnityEngine;

// Filtro complementario para roll y pitch.
// Ejes del cuerpo (Unity): x derecha, y arriba, z adelante.
// Roll positivo = lado derecho abajo. Pitch positivo = nariz arriba.
public class ComplementaryFilter
{
    public float tau = 0.3f;   // constante de tiempo: por debajo confía en el giroscopio, por encima en el acelerómetro

    public float Roll { get; private set; }    // rad
    public float Pitch { get; private set; }   // rad

    bool initialized;

    public void Reset()
    {
        initialized = false;
        Roll = 0f;
        Pitch = 0f;
    }

    // gyro en rad/s, accel en m/s² (fuerza específica, como la mide un MPU6050)
    public void Update(Vector3 gyro, Vector3 accel, float dt)
    {
        float rollAcc = -Mathf.Atan2(accel.x, accel.y);
        float pitchAcc = Mathf.Atan2(accel.z, accel.y);

        if (!initialized)
        {
            Roll = rollAcc;
            Pitch = pitchAcc;
            initialized = true;
            return;
        }

        float rollRate = -gyro.z;
        float pitchRate = -gyro.x;

        float alpha = tau / (tau + dt);
        Roll = alpha * (Roll + rollRate * dt) + (1f - alpha) * rollAcc;
        Pitch = alpha * (Pitch + pitchRate * dt) + (1f - alpha) * pitchAcc;
    }
}
