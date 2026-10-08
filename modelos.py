"""Funciones de transferencia del dron: planta, lazo interno, cascada y perturbación."""
import control as ct

from config import GAINS, J_RP, K_BANCO, K_RP, K_Y, KM, MASA, TAU_F, TAU_M, TD

s = ct.tf("s")


def lazo_velocidad(K, kp, ki):
    """Lazo interno: PI de velocidad angular sobre la planta K / (s (τm s + 1))."""
    planta = K / (s * (TAU_M * s + 1))
    L = (kp + ki / s) * planta
    return planta, L, ct.feedback(L, 1)


def eje_cascada(K, J, g_ang, g_vel):
    """Cascada de roll o pitch: P de ángulo afuera, PI de velocidad adentro."""
    planta, L_rate, T_rate = lazo_velocidad(K, g_vel["Kp"], g_vel["Ki"])
    L_angle = g_ang["Kp"] * T_rate / s
    T_angle = ct.minreal(ct.feedback(L_angle, 1), verbose=False)

    num_d, den_d = ct.pade(TD, 2)
    T_total = ct.tf(num_d, den_d) / (TAU_F * s + 1) * T_angle    # con el mando gestual

    # torque externo d -> ángulo
    C_rate = g_vel["Kp"] + g_vel["Ki"] / s
    G_act = J * K / (TAU_M * s + 1)
    T_dist = ct.minreal(ct.feedback(1 / (J * s**2), G_act * C_rate * (g_ang["Kp"] + s)),
                        verbose=False)
    return {"G_rate": planta, "L_rate": L_rate, "T_rate": T_rate, "L_angle": L_angle,
            "T_angle": T_angle, "T_total": T_total, "T_dist": T_dist}


def construir_modelos(g=GAINS):
    M = {
        "rp": eje_cascada(K_RP, J_RP, g["angulo"], g["velocidad"]),
        "banco": eje_cascada(K_BANCO, J_RP, g["angulo"], g["velocidad"]),
    }
    _, L_y, T_y = lazo_velocidad(K_Y, g["yaw"]["Kp"], g["yaw"]["Ki"])
    M["yaw"] = {"L_rate": L_y, "T_rate": ct.minreal(T_y, verbose=False)}
    M["altura"] = 4 * KM / (MASA * s**2 * (TAU_M * s + 1))
    return M
