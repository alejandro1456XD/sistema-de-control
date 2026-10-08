"""Estilo y gráficas del análisis (escalón, perturbación, lugar de las raíces, Bode, polos)."""
import control as ct
import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

from config import GAINS, OUT
from estabilidad import den_coeffs
from modelos import s

C_BLUE, C_ORANGE, C_AQUA = "#2a78d6", "#eb6834", "#1baf7a"
C_REF, C_GRID, C_TEXT = "#8a8985", "#e4e3df", "#52514e"
plt.rcParams.update({
    "figure.dpi": 130, "savefig.dpi": 160, "font.size": 10,
    "axes.edgecolor": C_GRID, "axes.labelcolor": C_TEXT,
    "axes.titleweight": "bold", "axes.titlesize": 11,
    "axes.spines.top": False, "axes.spines.right": False,
    "axes.grid": True, "grid.color": C_GRID, "grid.linewidth": 0.8,
    "xtick.color": C_TEXT, "ytick.color": C_TEXT,
    "lines.linewidth": 2, "legend.frameon": False,
})


def guardar(fig, nombre):
    fig.tight_layout()
    fig.savefig(OUT / nombre)
    plt.close(fig)
    print("  guardado:", OUT / nombre)


def graf_escalon(M, ref=15.0):
    t = np.linspace(0, 1.2, 2000)
    fig, ax = plt.subplots(figsize=(7, 4))
    ax.axhline(ref, color=C_REF, ls="--", lw=1.2, label="Referencia")
    _, y1 = ct.step_response(M["rp"]["T_angle"], t)
    _, y2 = ct.step_response(M["rp"]["T_total"], t)
    ax.plot(t, ref * y1, color=C_BLUE, label="Cascada")
    ax.plot(t, ref * y2, color=C_ORANGE, ls=(0, (5, 3)), label="Con mando gestual")
    ax.set(title=f"Roll / Pitch: escalón de {ref:.0f}°", xlabel="Tiempo [s]", ylabel="Ángulo [°]")
    ax.legend(loc="lower right")
    guardar(fig, "01_escalon_roll_pitch.png")


def graf_lazo_interno(M):
    t = np.linspace(0, 0.6, 2000)
    _, y = ct.step_response(M["rp"]["T_rate"], t)
    fig, ax = plt.subplots(figsize=(7, 4))
    ax.axhline(100, color=C_REF, ls="--", lw=1.2, label="Referencia")
    ax.plot(t, 100 * y, color=C_BLUE, label="Roll / Pitch")
    ax.set(title="Lazo interno: escalón de 100 °/s", xlabel="Tiempo [s]", ylabel="Velocidad [°/s]")
    ax.legend(loc="lower right")
    guardar(fig, "02_escalon_lazo_interno.png")


def graf_perturbacion(M, torque=1e-4):
    t = np.linspace(0, 6, 3000)
    _, y = ct.step_response(M["rp"]["T_dist"], t)
    fig, ax = plt.subplots(figsize=(7, 4))
    ax.axhline(0, color=C_REF, ls="--", lw=1.2)
    ax.plot(t, np.degrees(torque * y), color=C_BLUE)
    ax.set(title=f"Rechazo de un torque constante de {torque * 1e3:.1f} mN·m",
           xlabel="Tiempo [s]", ylabel="Desviación de ángulo [°]")
    guardar(fig, "03_perturbacion.png")


def graf_yaw(M):
    t = np.linspace(0, 2, 2000)
    _, y = ct.step_response(M["yaw"]["T_rate"], t)
    fig, ax = plt.subplots(figsize=(7, 4))
    ax.axhline(90, color=C_REF, ls="--", lw=1.2, label="Referencia")
    ax.plot(t, 90 * y, color=C_AQUA, label="Velocidad de yaw")
    ax.set(title="Yaw: escalón de 90 °/s", xlabel="Tiempo [s]", ylabel="Velocidad [°/s]")
    ax.legend(loc="lower right")
    guardar(fig, "04_escalon_yaw.png")


def graf_lugar_raices(M):
    L = M["rp"]["T_rate"] / s
    num, den = np.squeeze(L.num[0][0]), den_coeffs(L)
    ganancias = np.linspace(0.01, 60, 1500)
    raices = np.array([np.roots(np.polyadd(den, k * num)) for k in ganancias])
    kp = GAINS["angulo"]["Kp"]
    sel = ct.poles(ct.feedback(kp * L, 1))
    fig, ax = plt.subplots(figsize=(7, 5))
    for i in range(raices.shape[1]):
        ax.plot(raices[:, i].real, raices[:, i].imag, color=C_BLUE, lw=1.5)
    ax.scatter(ct.poles(L).real, ct.poles(L).imag, marker="x", s=60, color=C_TEXT,
               label="Polos de lazo abierto", zorder=3)
    ax.scatter(sel.real, sel.imag, s=50, color=C_ORANGE, edgecolor="white",
               label=f"Polos con Kpθ = {kp:g}", zorder=4)
    ax.axvline(0, color=C_REF, lw=1)
    ax.set(title="Lugar de las raíces del lazo de ángulo", xlabel="Parte real",
           ylabel="Parte imaginaria", xlim=(-40, 5))
    ax.legend(loc="upper left")
    guardar(fig, "05_lugar_raices_angulo.png")


def graf_bode(M):
    w = np.logspace(-1, 3, 800)
    fig, (a1, a2) = plt.subplots(2, 1, figsize=(7, 6), sharex=True)
    for L, color, etiqueta in [(M["rp"]["L_rate"], C_BLUE, "Lazo interno"),
                               (M["rp"]["L_angle"], C_ORANGE, "Lazo externo")]:
        mag, fase, _ = ct.frequency_response(L, w)
        a1.semilogx(w, 20 * np.log10(mag), color=color, label=etiqueta)
        a2.semilogx(w, np.degrees(np.unwrap(fase)), color=color, label=etiqueta)
    a1.axhline(0, color=C_REF, ls="--", lw=1)
    a2.axhline(-180, color=C_REF, ls="--", lw=1)
    a1.set(title="Bode de lazo abierto (roll y pitch son iguales)", ylabel="Magnitud [dB]")
    a2.set(xlabel="Frecuencia [rad/s]", ylabel="Fase [°]")
    a1.legend(loc="upper right")
    guardar(fig, "06_bode.png")


def graf_polos(M):
    fig, ax = plt.subplots(figsize=(7, 5))
    for sys, color, etiqueta in [(M["rp"]["T_angle"], C_BLUE, "Roll / Pitch"),
                                 (M["yaw"]["T_rate"], C_AQUA, "Yaw")]:
        p = ct.poles(sys)
        ax.scatter(p.real, p.imag, s=55, color=color, edgecolor="white", label=etiqueta, zorder=3)
    ax.axvline(0, color=C_REF, lw=1)
    ax.set(title="Polos de lazo cerrado", xlabel="Parte real", ylabel="Parte imaginaria")
    ax.legend(loc="upper left")
    guardar(fig, "07_mapa_polos.png")
