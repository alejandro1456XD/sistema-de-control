"""Parámetros del dron y del controlador (los mismos que usa Unity)."""
from pathlib import Path

AQUI = Path(__file__).resolve().parent
OUT = AQUI / "resultados"          # aquí se guardan las gráficas y params.json
OUT.mkdir(exist_ok=True)

G = 9.81
MASA = 0.06            # kg
J_RP = 3e-5            # inercia roll / pitch [kg·m²]
J_Y = 5e-5             # inercia yaw [kg·m²]
F_MAX = 0.3            # empuje máximo por motor [N]
BRAZO = 0.04           # distancia de cada motor al eje [m]
BRAZO_BANCO = 0.0425   # brazo efectivo del banco de pruebas [m]
YAW_COEF = 0.00375     # par de reacción por newton de empuje [m]
TAU_M = 0.03           # constante de tiempo del motor [s]
TAU_F, TD = 0.05, 0.015  # mando gestual: filtro y retardo [s]
HOVER = MASA * G / (4 * F_MAX)   # throttle de hover (0.4905)

K_RP = 4 * F_MAX * BRAZO / J_RP          # 1600
K_Y = 4 * F_MAX * YAW_COEF / J_Y         # 90
K_BANCO = 4 * F_MAX * BRAZO_BANCO / J_RP  # 1700
KM = F_MAX                               # empuje por unidad de throttle [N]

# Ganancias (ángulos en rad, velocidades en rad/s)
GAINS = {
    "angulo": {"Kp": 10.0},
    "velocidad": {"Kp": 0.03, "Ki": 0.05},
    "yaw": {"Kp": 0.2, "Ki": 0.05},
}
OS_MAX = 10.0   # sobrepico máximo [%]
TS_MAX = 0.5    # t. establecimiento máximo de roll / pitch [s]
