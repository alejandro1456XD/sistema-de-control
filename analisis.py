"""Modo "analisis": modelos, estabilidad, criterios de diseño y gráficas."""
import json

import control as ct

from config import (GAINS, HOVER, J_RP, J_Y, K_BANCO, K_RP, K_Y, KM, MASA, OS_MAX, OUT,
                    TAU_F, TAU_M, TD, TS_MAX)
from estabilidad import describir, imprimir_routh, margenes
from graficas import (graf_bode, graf_escalon, graf_lazo_interno, graf_lugar_raices,
                      graf_perturbacion, graf_polos, graf_yaw)
from modelos import construir_modelos


def exportar_params():
    """params.json con el formato que lee Unity (Assets/Datos/params.json)."""
    datos = {
        "planta": {"J": J_RP, "K": K_BANCO, "tau_m": TAU_M, "Ky": K_Y, "m": MASA, "Km": KM},
        "mando": {"tau_f": TAU_F, "Td": TD},
        "ganancias": {
            "rate": {"Kp": GAINS["velocidad"]["Kp"], "Ki": GAINS["velocidad"]["Ki"], "Kd": 0.0},
            "angle": {"Kp": GAINS["angulo"]["Kp"]},
            "yaw": {"Kp": GAINS["yaw"]["Kp"], "Ki": GAINS["yaw"]["Ki"]},
        },
        "control_dt": 0.002,
    }
    ruta = OUT / "params.json"
    ruta.write_text(json.dumps(datos, indent=2), encoding="utf-8")
    print("  guardado:", ruta)


def analisis():
    M = construir_modelos()
    print("=" * 70)
    print("ANÁLISIS DEL SISTEMA DE CONTROL: VANT CONTROLADO POR GESTOS")
    print("=" * 70)
    print(f"\nMasa {MASA * 1000:.0f} g   J roll/pitch {J_RP:.1e}   J yaw {J_Y:.1e} kg·m²")
    print(f"Hover: throttle {HOVER:.4f}   Km = {KM} N por unidad")
    print(f"K roll/pitch = {K_RP:.0f}   K yaw = {K_Y:.0f}   K banco = {K_BANCO:.0f}")

    estables = [
        describir("Roll/Pitch: lazo interno ω/ω_ref", M["rp"]["T_rate"]),
        describir("Roll/Pitch: cascada θ/θ_ref", M["rp"]["T_angle"]),
        describir("Roll/Pitch: con mando θ/θ_mano", M["rp"]["T_total"]),
        describir("Yaw ψ̇/ψ̇_ref", M["yaw"]["T_rate"]),
    ]
    print("\n--- Altura Z/ΔT (lazo abierto) ---")
    print(M["altura"])
    print("\n--- Perturbación θ/d ---")
    print(M["rp"]["T_dist"])
    print(f"Ganancia DC: {ct.dcgain(M['rp']['T_dist']):.3e}  (≈ 0 -> error estacionario nulo)")

    imprimir_routh("Roll/Pitch (cascada)", M["rp"]["T_angle"])
    imprimir_routh("Yaw", M["yaw"]["T_rate"])

    pm_i, wc_i, _ = margenes(M["rp"]["L_rate"])
    pm_o, wc_o, gm_o = margenes(M["rp"]["L_angle"])
    print("\nMárgenes de estabilidad")
    print(f"  interno: MF = {pm_i:.1f}°  ωc = {wc_i:.1f} rad/s | "
          f"externo: MF = {pm_o:.1f}°  ωc = {wc_o:.1f} rad/s  MG = {gm_o:.1f} dB")

    print("\nCriterios de diseño")
    ir = ct.step_info(M["rp"]["T_total"])
    iy = ct.step_info(M["yaw"]["T_rate"])
    for nombre, info, ts_max in (("Roll/Pitch con mando", ir, TS_MAX), ("Yaw", iy, 1.0)):
        ok = info["Overshoot"] <= OS_MAX and info["SettlingTime"] <= ts_max
        print(f"  {nombre:22s}: sobrepico {info['Overshoot']:.2f}%  t_est {info['SettlingTime']:.3f} s"
              f"  -> {'CUMPLE' if ok else 'NO CUMPLE'}")

    print("\nGenerando gráficas...")
    graf_escalon(M)
    graf_lazo_interno(M)
    graf_perturbacion(M)
    graf_yaw(M)
    graf_lugar_raices(M)
    graf_bode(M)
    graf_polos(M)
    exportar_params()
    print("\nResultado:", "TODOS LOS LAZOS SON ESTABLES" if all(estables) else "HAY LAZOS INESTABLES")
