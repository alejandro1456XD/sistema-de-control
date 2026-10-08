"""Modo "comparar": respuesta teórica contra los datos de la simulación en Unity."""
import control as ct
import matplotlib.pyplot as plt
import numpy as np

from datos_unity import CSV_DRON, CSV_ESCALON, CSV_PERTURBACION, leer_csv
from graficas import C_AQUA, C_BLUE, C_ORANGE, C_REF, guardar
from modelos import construir_modelos


def metricas(t, y, amp, t0):
    m = t >= t0
    tt, yy = t[m] - t0, y[m]
    final = yy[-3:].mean()
    os_ = max(0.0, (yy.max() - amp) / amp * 100)
    fuera = np.where(np.abs(yy - amp) > 0.02 * amp)[0]
    ts = tt[min(fuera[-1] + 1, len(tt) - 1)] if len(fuera) else 0.0
    return os_, ts, final


def curva_teorica(tf, amp, t, t0, dur=1.0):
    tm = np.linspace(0, dur, 1500)
    _, y = ct.step_response(tf * amp, tm)
    return tm, y, np.interp(t - t0, tm, y, left=0)


# ===========================================================================
# 6. COMPARACIÓN TEORÍA vs UNITY
# ===========================================================================
def comparar_banco(M):
    d = leer_csv(CSV_ESCALON)
    t, ref = d["t"], d["ref_deg"]
    paso = np.where(np.diff(ref) != 0)[0][-1] + 1
    t0, amp = t[paso], ref[paso] - ref[paso - 1]
    tm, y_teo, teo = curva_teorica(M["banco"]["T_angle"], amp, t, t0, t[-1] - t0)

    filas = [("Teoría (cascada)",) + metricas(t0 + tm, y_teo, amp, t0) + (0.0,)]
    for nombre, col in (("Unity real", "theta_real_deg"), ("Unity estimado", "theta_est_deg")):
        y = d[col]
        rms = np.sqrt(np.mean((y[t >= t0] - teo[t >= t0]) ** 2))
        filas.append((nombre,) + metricas(t, y, amp, t0) + (rms,))

    fig, ax = plt.subplots(1, 2, figsize=(11, 4.2))
    for a in ax:
        a.step(t, ref, where="post", color=C_REF, ls="--", label="Referencia")
        a.plot(t0 + tm, y_teo, color=C_BLUE, label="Teoría")
        a.plot(t, d["theta_real_deg"], color=C_ORANGE, label="Unity (real)")
        a.plot(t, d["theta_est_deg"], color=C_AQUA, lw=1.2, label="Unity (estimado)")
        a.set(xlabel="t [s]", ylabel="Ángulo de roll [°]")
    ax[0].set_title("Banco 1-GDL: escalón de 15°, teoría vs Unity")
    ax[0].legend()
    ax[1].set_xlim(t0 - 0.05, t0 + 0.8)
    ax[1].set_title("Detalle de la subida")
    guardar(fig, "08_comparacion_banco_escalon.png")

    print("\nBANCO 1-GDL: ESCALÓN DE 15°")
    print(f"{'':20s}{'OS [%]':>8s}{'ts [s]':>9s}{'final [°]':>11s}{'RMS vs teo':>12s}")
    for n, os_, ts, fin, rms in filas:
        print(f"{n:20s}{os_:8.2f}{ts:9.3f}{fin:11.3f}{rms:12.3f}")


def comparar_perturbacion(M, torque=1e-4, t_on=0.5):
    d = leer_csv(CSV_PERTURBACION)
    t, y = d["t"], d["theta_real_deg"]
    tm = np.linspace(0, t[-1] - t_on, 4000)
    _, yt = ct.step_response(M["banco"]["T_dist"] * torque, tm)
    yt = np.degrees(yt)

    fig, ax = plt.subplots(figsize=(6.5, 4.2))
    ax.plot(t_on + tm, yt, color=C_BLUE, label="Teoría")
    ax.plot(t, y, color=C_ORANGE, label="Unity (real)")
    ax.axvline(t_on, color=C_REF, ls="--", lw=1)
    ax.set(title="Perturbación de 1e-4 N·m desde t = 0.5 s", xlabel="t [s]",
           ylabel="Ángulo de roll [°]")
    ax.legend()
    guardar(fig, "09_comparacion_banco_perturbacion.png")

    print("\nBANCO 1-GDL: PERTURBACIÓN")
    print(f"  pico:  teoría {yt.max():.3f}°   Unity {y[t >= t_on].max():.3f}°")
    print(f"  final: teoría {yt[-1]:.3f}°   Unity {y[-5:].mean():.3f}°  (incluye el sesgo del giroscopio)")


def comparar_dron(M):
    d = leer_csv(CSV_DRON)
    t = d["t"]
    ejes = [
        ("Roll (escalón 15°)", M["rp"]["T_angle"], "roll_ref", 1.0, 15.0, "°",
         [("Unity estimado", "roll_est", C_AQUA), ("Unity real", "roll_real", C_ORANGE)]),
        ("Pitch (escalón 15°)", M["rp"]["T_angle"], "pitch_ref", 4.0, 15.0, "°",
         [("Unity estimado", "pitch_est", C_AQUA), ("Unity real", "pitch_real", C_ORANGE)]),
        ("Yaw (velocidad 90 °/s)", M["yaw"]["T_rate"], "yaw_ref", 7.0, 90.0, "°/s",
         [("Unity real", "yaw_rate", C_ORANGE)]),
    ]
    fig, axs = plt.subplots(1, 3, figsize=(15, 4.2))
    filas = []
    for ax, (nombre, tf, col_ref, t0, amp, unidad, medidas) in zip(axs, ejes):
        m = (t >= t0 - 0.08) & (t <= t0 + 0.95)
        ts_, ref = t[m], d[col_ref][m]
        tm, y_teo, teo = curva_teorica(tf, amp, ts_, t0)
        filas.append((nombre, "Teoría") + metricas(t0 + tm, y_teo, amp, t0) + (0.0,))
        ax.step(ts_, ref, where="post", color=C_REF, ls="--", label="Referencia")
        ax.plot(t0 + tm, y_teo, color=C_BLUE, label="Teoría")
        for etiqueta, col, color in medidas:
            y = d[col][m]
            rms = np.sqrt(np.mean((y[ts_ >= t0] - teo[ts_ >= t0]) ** 2))
            filas.append((nombre, etiqueta) + metricas(ts_, y, amp, t0) + (rms,))
            ax.plot(ts_, y, color=color, label=etiqueta)
        ax.set(title=nombre, xlabel="t [s]", ylabel=f"[{unidad}]")
    axs[0].legend()
    guardar(fig, "10_comparacion_dron_completo.png")

    print("\nDRON COMPLETO")
    print(f"{'Eje':24s}{'Curva':16s}{'OS [%]':>8s}{'ts [s]':>9s}{'final':>9s}{'RMS vs teo':>12s}")
    for n, c, os_, ts, fin, rms in filas:
        print(f"{n:24s}{c:16s}{os_:8.2f}{ts:9.3f}{fin:9.2f}{rms:12.3f}")

    print("\nInterferencia entre ejes (máx. cambio del otro eje durante cada maniobra)")
    for nombre, t0, t1, cols in (("roll", 1.0, 2.0, ["pitch_real", "yaw_rate"]),
                                 ("pitch", 4.0, 5.0, ["roll_real", "yaw_rate"]),
                                 ("yaw", 7.0, 8.0, ["roll_real", "pitch_real"])):
        w = (t >= t0) & (t <= t1)
        base = np.where(t <= t0)[0][-1]
        cambio = {c: float(np.abs(d[c][w] - d[c][base]).max()) for c in cols}
        print(f"  durante {nombre}: " + ", ".join(f"{c} {v:.2f}" for c, v in cambio.items()))


def comparar():
    M = construir_modelos()
    comparar_banco(M)
    comparar_perturbacion(M)
    comparar_dron(M)
