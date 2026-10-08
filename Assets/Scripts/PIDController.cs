using UnityEngine;

// PID con derivada filtrada y anti-windup por integración condicional.
// Solo usa float y matemática simple para poder pasarlo casi igual al ESP32.
public class PIDController
{
    public float kp;
    public float ki;
    public float kd;
    public float derivativeTau = 0.01f;   // filtro de la derivada [s]
    public float outputMin = -1f;
    public float outputMax = 1f;
    public float integralLimit = 1f;      // máximo aporte del término I a la salida

    float integral;
    float prevError;
    float dFiltered;
    bool hasPrev;

    public PIDController(float kp, float ki, float kd, float outputMin, float outputMax)
    {
        this.kp = kp;
        this.ki = ki;
        this.kd = kd;
        this.outputMin = outputMin;
        this.outputMax = outputMax;
    }

    public void Reset()
    {
        integral = 0f;
        prevError = 0f;
        dFiltered = 0f;
        hasPrev = false;
    }

    public float Update(float error, float dt)
    {
        if (dt <= 0f) return 0f;

        float p = kp * error;

        float rawD = hasPrev ? (error - prevError) / dt : 0f;
        dFiltered += (rawD - dFiltered) * (dt / (derivativeTau + dt));
        float d = kd * dFiltered;
        prevError = error;
        hasPrev = true;

        // Si la salida ya está saturada hacia el lado del error, no se sigue integrando
        float unsat = p + ki * (integral + error * dt) + d;
        bool saturaArriba = unsat > outputMax && error > 0f;
        bool saturaAbajo = unsat < outputMin && error < 0f;
        if (!saturaArriba && !saturaAbajo) integral += error * dt;

        if (ki > 0f)
        {
            float lim = integralLimit / ki;
            integral = Mathf.Clamp(integral, -lim, lim);
        }

        float output = p + ki * integral + d;
        return Mathf.Clamp(output, outputMin, outputMax);
    }
}
