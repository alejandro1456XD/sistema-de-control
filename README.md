# VANT controlado por gestos

Cada archivo hace una sola cosa:

| Archivo | Qué hace |
|---|---|
| `main.py` | Punto de entrada. Elige el modo según lo que escribas. |
| `config.py` | Parámetros del dron y ganancias del controlador (los mismos que Unity). |
| `modelos.py` | Funciones de transferencia: planta, lazo interno, cascada, perturbación. |
| `estabilidad.py` | Routh-Hurwitz, polos, sobrepico y márgenes. |
| `graficas.py` | Estilo y gráficas del análisis. |
| `analisis.py` | Modo `analisis`: junta modelos, estabilidad, criterios y gráficas. |
| `datos_unity.py` | Datos muestreados de las simulaciones en Unity. |
| `comparacion.py` | Modo `comparar`: teoría contra Unity. |
| `mapeo_gestos.py` | Cálculo de la mano a referencias del dron (sin cámara). |
| `gestos.py` | Modo `gestos`: cámara, MediaPipe y envío UDP a Unity. |

## Uso

```
pip install -r requirements.txt
python main.py analisis
python main.py comparar
python main.py todo
python main.py gestos
```

Las gráficas se guardan en la carpeta `resultados`.
