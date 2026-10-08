"""Modo "gestos": lee la cámara con MediaPipe y manda las referencias a Unity por UDP."""
import socket
import time
import urllib.request

from config import AQUI, HOVER
from mapeo_gestos import (CUADROS_CALIBRACION, MAX_ANGULO, MAX_YAW, RANGO_BAJADA, RANGO_PROF,
                          RANGO_SUBIDA, ZONA_MUERTA_GAS, ZONA_MUERTA_LATERAL, ZONA_MUERTA_PROF,
                          FILTRO_GESTO, angulos_mano, clamp, curva, tamano, zona)

MODELO_URL = ("https://storage.googleapis.com/mediapipe-models/hand_landmarker/"
              "hand_landmarker/float16/1/hand_landmarker.task")


def _descargar_modelo():
    ruta = AQUI / "hand_landmarker.task"
    if not ruta.exists():
        print("Descargando modelo de manos (una sola vez)...")
        urllib.request.urlretrieve(MODELO_URL, ruta)
    return str(ruta)


def gestos(cam_id=0, host="127.0.0.1", puerto=5005):
    """Palma hacia la cámara y dedos arriba. El dron empieza apagado en el suelo.
    Subir la mano: el dron despega y sube.  Bajarla: desciende despacio.  Neutro: mantiene la altura.
    Derecha / izquierda: inclinar o mover la mano hacia ese lado (roll).
    Adelante / atrás: acercar la mano a la cámara (o inclinar los dedos hacia ella) avanza;
    alejarla (o inclinar los dedos hacia atrás) retrocede.
    Al empezar, mantén la mano quieta en posición neutra un segundo: se calibra sola.
    Teclas: C = recalibrar, + / - = sensibilidad, A / D = girar (yaw), Q o Esc = salir.
    Sin mano el dron mantiene la altura (en el suelo, se queda apagado)."""
    import cv2
    import mediapipe as mp
    from mediapipe.tasks import python as mp_python
    from mediapipe.tasks.python import vision

    opciones = vision.HandLandmarkerOptions(
        base_options=mp_python.BaseOptions(model_asset_path=_descargar_modelo()),
        running_mode=vision.RunningMode.VIDEO, num_hands=1,
        min_hand_detection_confidence=0.6, min_tracking_confidence=0.6)
    detector = vision.HandLandmarker.create_from_options(opciones)

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    cam = cv2.VideoCapture(cam_id)
    if not cam.isOpened():
        raise SystemExit(f"No se pudo abrir la cámara {cam_id}")

    neutro = {"roll": 0.0, "pitch": 0.0, "x": 0.5, "y": 0.5, "tam": 0.2}
    suave = {"roll": 0.0, "pitch": 0.0, "yaw": 0.0, "gas": HOVER}
    crudo = None
    sens = 1.0
    yaw_tecla, t_yaw = 0.0, 0.0
    calibrado = False
    cuadros_mano = 0
    t0 = time.time()

    while True:
        ok, frame = cam.read()
        if not ok:
            break
        frame = cv2.flip(frame, 1)
        img = mp.Image(image_format=mp.ImageFormat.SRGB,
                       data=cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
        res = detector.detect_for_video(img, int((time.time() - t0) * 1000))

        mano = bool(res.hand_landmarks)
        if mano:
            lm = res.hand_landmarks[0]
            r, p = angulos_mano(lm)
            cx, cy = lm[9].x, lm[9].y
            crudo = {"roll": r, "pitch": p, "x": cx, "y": cy, "tam": tamano(lm)}

            if not calibrado:
                cuadros_mano += 1
                if cuadros_mano >= CUADROS_CALIBRACION:
                    neutro = dict(crudo)
                    calibrado = True
                    print("Calibrado:", {k: round(v, 2) for k, v in neutro.items()})
                objetivo = {"roll": 0.0, "pitch": 0.0, "yaw": 0.0, "gas": HOVER}
            else:
                # Inclinar o mover la mano hacia un lado inclina el dron hacia ese lado
                lateral = zona(cx - neutro["x"], ZONA_MUERTA_LATERAL, 0.25)
                alto = zona(neutro["y"] - cy, ZONA_MUERTA_GAS, 0.2)
                prof = zona(crudo["tam"] / neutro["tam"] - 1.0, ZONA_MUERTA_PROF, RANGO_PROF)
                objetivo = {
                    "roll": clamp(curva(r - neutro["roll"], sens) + MAX_ANGULO * sens * lateral,
                                   -MAX_ANGULO, MAX_ANGULO),
                    "pitch": clamp(curva(p - neutro["pitch"], sens) - MAX_ANGULO * sens * prof,
                                    -MAX_ANGULO, MAX_ANGULO),
                    "yaw": yaw_tecla if time.time() - t_yaw < 0.25 else 0.0,
                    "gas": HOVER + (RANGO_SUBIDA if alto > 0 else RANGO_BAJADA) * alto,
                }
            for k in suave:
                suave[k] += FILTRO_GESTO * (objetivo[k] - suave[k])

            for pt in lm:
                cv2.circle(frame, (int(pt.x * frame.shape[1]), int(pt.y * frame.shape[0])),
                           3, (0, 255, 0), -1)
        else:
            suave = {"roll": 0.0, "pitch": 0.0, "yaw": 0.0, "gas": HOVER}
            if not calibrado:
                cuadros_mano = 0

        msg = (f"{suave['roll']:.2f},{suave['pitch']:.2f},{suave['yaw']:.1f},"
               f"{suave['gas']:.4f},{1 if mano else 0}")
        sock.sendto(msg.encode("ascii"), (host, puerto))

        estado = (f"roll {suave['roll']:+5.1f}  pitch {suave['pitch']:+5.1f}  "
                  f"yaw {suave['yaw']:+5.0f}  gas {suave['gas']:.2f}  sens {sens:.1f}") if mano else "sin mano: hover"
        if mano and not calibrado:
            estado = f"calibrando... mano quieta ({cuadros_mano}/{CUADROS_CALIBRACION})"
        h, w = frame.shape[:2]
        cv2.putText(frame, estado, (10, 28), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 255), 2)
        cv2.putText(frame, "C: recalibrar  +/-: sensibilidad  A/D: yaw  Q: salir", (10, h - 12),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 1)
        cv2.drawMarker(frame, (int(neutro["x"] * w), int(neutro["y"] * h)),
                       (255, 0, 255), cv2.MARKER_CROSS, 20, 1)
        cv2.imshow("Mando gestual", frame)

        tecla = cv2.waitKey(1) & 0xFF
        if tecla in (ord("q"), 27):
            break
        if tecla == ord("c") and crudo is not None:
            neutro = dict(crudo)
            calibrado = True
            print("Calibrado:", {k: round(v, 2) for k, v in neutro.items()})
        if tecla in (ord("a"), ord("d")):
            yaw_tecla = MAX_YAW * 0.5 * (1 if tecla == ord("d") else -1)
            t_yaw = time.time()
        if tecla in (ord("+"), ord("=")):
            sens = min(2.0, sens + 0.1)
        if tecla in (ord("-"), ord("_")):
            sens = max(0.2, sens - 0.1)

    sock.sendto(f"0,0,0,{HOVER:.4f},0".encode("ascii"), (host, puerto))
    cam.release()
    cv2.destroyAllWindows()
