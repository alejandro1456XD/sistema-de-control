using UnityEngine;

// La cámara sigue al dron manteniendo la distancia inicial y siempre lo mira.
public class CamaraSigue : MonoBehaviour
{
    public Transform objetivo;
    public float suavizado = 4f;

    Vector3 desfase;

    void Start()
    {
        if (objetivo == null)
        {
            var d = GameObject.Find("Dron");
            if (d != null) objetivo = d.transform;
        }
        if (objetivo != null) desfase = transform.position - objetivo.position;
    }

    void LateUpdate()
    {
        if (objetivo == null) return;
        Vector3 destino = objetivo.position + desfase;
        transform.position = Vector3.Lerp(transform.position, destino, 1f - Mathf.Exp(-suavizado * Time.deltaTime));
        transform.LookAt(objetivo.position + Vector3.up * 0.1f);
    }
}
