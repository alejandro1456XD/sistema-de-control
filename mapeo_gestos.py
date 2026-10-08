"""Mapeo de la mano a las referencias del dron (sin cámara ni red, solo cálculo)."""
import math

MAX_ANGULO = 20.0        # grados máximos de roll y pitch del dron
RANGO_MANO = 40.0        # grados de inclinación de la mano para llegar al máximo
ZONA_MUERTA_ANG = 5.0    # grados de la mano que se ignoran alrededor del neutro
EXPO = 0.5               # 0 = lineal, 1 = muy suave cerca del centro
MAX_YAW = 90.0           # °/s máximos
RANGO_SUBIDA = 0.05      # throttle por encima del hover con la mano arriba del todo
RANGO_BAJADA = 0.02      # throttle por debajo del hover con la mano abajo (baja lento)
ZONA_MUERTA_PROF = 0.08     # cambio relativo del tamaño de la mano que se ignora (acercar / alejar)
RANGO_PROF = 0.30           # cambio relativo del tamaño de la mano para llegar al máximo
ZONA_MUERTA_LATERAL = 0.08  # fracción de la imagen alrededor del centro sin movimiento lateral
ZONA_MUERTA_GAS = 0.06   # fracción de la imagen alrededor del centro sin cambio de throttle
FILTRO_GESTO = 0.2       # 0..1: más bajo = más suave pero más lento
CUADROS_CALIBRACION = 30  # cuadros con la mano quieta para calibrar solo al empezar


def clamp(x, a, b):
    return max(a, min(b, x))


def curva(delta, sens):
    """Convierte la inclinación de la mano (grados) en ángulo del dron: zona muerta + curva suave."""
    x = (abs(delta) - ZONA_MUERTA_ANG) / (RANGO_MANO - ZONA_MUERTA_ANG)
    x = clamp(x, 0.0, 1.0)
    salida = MAX_ANGULO * sens * (EXPO * x**3 + (1 - EXPO) * x)
    return math.copysign(salida, delta)


def zona(valor, zona, rango):
    """Zona muerta y escala 0..1 para un desplazamiento en la imagen."""
    if abs(valor) <= zona:
        return 0.0
    return math.copysign(min(1.0, (abs(valor) - zona) / rango), valor)


def angulos_mano(lm):
    """(roll, pitch) crudos en grados a partir de los puntos de la mano."""
    base, nudillo = lm[0], lm[9]
    dx, dy = nudillo.x - base.x, base.y - nudillo.y
    roll = math.degrees(math.atan2(dx, dy))
    largo = math.hypot(dx, dy) + 1e-6
    dz = lm[12].z - base.z      # z menor = más cerca de la cámara
    # dedos hacia la cámara (z menor) = hacia delante = pitch negativo (nariz abajo)
    pitch = math.degrees(math.asin(clamp(dz / (largo * 2.2), -1, 1)))
    return roll, pitch


def tamano(lm):
    """Tamaño de la mano en la imagen: crece al acercarla a la cámara."""
    ancho = math.hypot(lm[5].x - lm[17].x, lm[5].y - lm[17].y)
    largo = math.hypot(lm[9].x - lm[0].x, lm[9].y - lm[0].y)
    return (ancho + largo) / 2
