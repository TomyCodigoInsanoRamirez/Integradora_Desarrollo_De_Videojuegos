using UnityEngine;

// Capa de fondo con parallax: se mueve una fracción del movimiento de la cámara.
// followX/followY = 1 se pega a la cámara (parece infinitamente lejos), 0 se queda fija en el mundo,
// y un valor negativo la mueve en sentido contrario (primer plano, se ve pasar más rápido que el suelo).
// Se ejecuta después de CameraFollow para que no tiemble.
[DefaultExecutionOrder(100)]
public class ParallaxLayer : MonoBehaviour
{
    [Range(-1f, 1f)] [SerializeField] private float followX = 0.5f;
    [Range(-1f, 1f)] [SerializeField] private float followY = 0f;

    private Transform cam;
    private Vector3 startPos;
    private Vector3 camStart;

    private void Start()
    {
        if (Camera.main == null) { enabled = false; return; }
        cam = Camera.main.transform;
        startPos = transform.position;
        camStart = cam.position;
    }

    private void LateUpdate()
    {
        Vector3 delta = cam.position - camStart;
        transform.position = new Vector3(
            startPos.x + delta.x * followX,
            startPos.y + delta.y * followY,
            startPos.z);
    }
}
