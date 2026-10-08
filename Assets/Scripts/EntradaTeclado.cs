using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum Tecla
{
    Izquierda, Derecha, Arriba, Abajo, Espacio, Cero, Uno, Dos, Perturbacion, Reiniciar,
    YawIzq, YawDer, GasMas, GasMenos, Congelar
}

// Lee el teclado con el Input System nuevo o con el clásico, según lo que tenga el proyecto.
public static class EntradaTeclado
{
    // Verdadero solo en el cuadro en que se presiona la tecla
    public static bool Presionada(Tecla t)
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return false;
        switch (t)
        {
            case Tecla.Izquierda: return k.leftArrowKey.wasPressedThisFrame;
            case Tecla.Derecha: return k.rightArrowKey.wasPressedThisFrame;
            case Tecla.Arriba: return k.upArrowKey.wasPressedThisFrame;
            case Tecla.Abajo: return k.downArrowKey.wasPressedThisFrame;
            case Tecla.Espacio: return k.spaceKey.wasPressedThisFrame;
            case Tecla.Cero: return k.digit0Key.wasPressedThisFrame;
            case Tecla.Uno: return k.digit1Key.wasPressedThisFrame;
            case Tecla.Dos: return k.digit2Key.wasPressedThisFrame;
            case Tecla.Perturbacion: return k.gKey.wasPressedThisFrame;
            case Tecla.Reiniciar: return k.rKey.wasPressedThisFrame;
            case Tecla.YawIzq: return k.qKey.wasPressedThisFrame;
            case Tecla.YawDer: return k.eKey.wasPressedThisFrame;
            case Tecla.GasMas: return k.wKey.wasPressedThisFrame;
            case Tecla.GasMenos: return k.sKey.wasPressedThisFrame;
            case Tecla.Congelar: return k.fKey.wasPressedThisFrame;
        }
        return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
        switch (t)
        {
            case Tecla.Izquierda: return Input.GetKeyDown(KeyCode.LeftArrow);
            case Tecla.Derecha: return Input.GetKeyDown(KeyCode.RightArrow);
            case Tecla.Arriba: return Input.GetKeyDown(KeyCode.UpArrow);
            case Tecla.Abajo: return Input.GetKeyDown(KeyCode.DownArrow);
            case Tecla.Espacio: return Input.GetKeyDown(KeyCode.Space);
            case Tecla.Cero: return Input.GetKeyDown(KeyCode.Alpha0);
            case Tecla.Uno: return Input.GetKeyDown(KeyCode.Alpha1);
            case Tecla.Dos: return Input.GetKeyDown(KeyCode.Alpha2);
            case Tecla.Perturbacion: return Input.GetKeyDown(KeyCode.G);
            case Tecla.Reiniciar: return Input.GetKeyDown(KeyCode.R);
            case Tecla.YawIzq: return Input.GetKeyDown(KeyCode.Q);
            case Tecla.YawDer: return Input.GetKeyDown(KeyCode.E);
            case Tecla.GasMas: return Input.GetKeyDown(KeyCode.W);
            case Tecla.GasMenos: return Input.GetKeyDown(KeyCode.S);
            case Tecla.Congelar: return Input.GetKeyDown(KeyCode.F);
        }
        return false;
#else
        return false;
#endif
    }
}
