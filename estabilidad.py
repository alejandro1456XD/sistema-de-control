"""Análisis de estabilidad: Routh-Hurwitz, polos, sobrepico y márgenes."""
import control as ct
import numpy as np


def routh(coeffs, eps=1e-9):
    c = np.array(coeffs, dtype=float)
    n = len(c)
    cols = (n + 1) // 2
    tabla = np.zeros((n, cols))
    tabla[0, : len(c[0::2])] = c[0::2]
    tabla[1, : len(c[1::2])] = c[1::2]
    for i in range(2, n):
        if abs(tabla[i - 1, 0]) < eps:
            tabla[i - 1, 0] = eps
        for j in range(cols - 1):
            a, b = tabla[i - 2, 0], tabla[i - 1, 0]
            tabla[i, j] = (b * tabla[i - 2, j + 1] - a * tabla[i - 1, j + 1]) / b
    return tabla, int(np.sum(np.diff(np.sign(tabla[:, 0])) != 0))


def den_coeffs(sys):
    return np.atleast_1d(np.squeeze(sys.den[0][0]))


def imprimir_routh(nombre, sys):
    tabla, cambios = routh(den_coeffs(sys))
    print(f"\nTabla de Routh — {nombre}")
    n = len(tabla)
    for i, fila in enumerate(tabla):
        print(f"  s^{n - 1 - i}: " + "  ".join(f"{v:12.4g}" for v in fila))
    print("  Cambios de signo:", cambios, "->", "ESTABLE" if cambios == 0 else "INESTABLE")


def describir(nombre, sys):
    polos = ct.poles(sys)
    estable = bool(np.all(polos.real < 0))
    print(f"\n--- {nombre} ---")
    print(sys)
    print("Polos:", np.round(polos, 3))
    print("Estable:", "SÍ" if estable else "NO")
    if estable:
        info = ct.step_info(sys)
        print(f"Sobrepico: {info['Overshoot']:.2f} %   t_subida: {info['RiseTime']:.3f} s   "
              f"t_establecimiento: {info['SettlingTime']:.3f} s")
    return estable


def margenes(L):
    gm, pm, _, wc = ct.margin(L)
    return float(pm), float(wc), (20 * np.log10(gm) if np.isfinite(gm) else float("inf"))
