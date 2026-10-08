"""
Proyecto VANT controlado por gestos.

Uso:
    python main.py analisis    # funciones de transferencia, estabilidad y gráficas
    python main.py comparar    # teoría vs simulación de Unity (datos incluidos)
    python main.py todo        # analisis + comparar
    python main.py gestos      # mando por cámara -> Unity (UDP, puerto 5005)

Para analisis y comparar:  pip install control numpy matplotlib
Para gestos:               pip install mediapipe opencv-python
"""
import argparse


def main():
    ap = argparse.ArgumentParser(description="VANT controlado por gestos: análisis, comparación y mando")
    ap.add_argument("modo", nargs="?", default="todo",
                    choices=["analisis", "comparar", "todo", "gestos"])
    ap.add_argument("--cam", type=int, default=0, help="índice de la cámara (modo gestos)")
    ap.add_argument("--host", default="127.0.0.1", help="IP de Unity (modo gestos)")
    ap.add_argument("--port", type=int, default=5005, help="puerto UDP (modo gestos)")
    a = ap.parse_args()

    if a.modo in ("analisis", "todo"):
        from analisis import analisis
        analisis()
    if a.modo in ("comparar", "todo"):
        from comparacion import comparar
        comparar()
    if a.modo == "gestos":
        from gestos import gestos
        gestos(a.cam, a.host, a.port)


if __name__ == "__main__":
    main()
