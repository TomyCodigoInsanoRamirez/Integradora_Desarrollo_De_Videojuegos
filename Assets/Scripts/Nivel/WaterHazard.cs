using UnityEngine;

// Agua profunda o ácido: si el jugador cae, pierde vida y reaparece en la orilla.
// Con dos puntos, vuelve a la orilla de donde saltó (según hacia dónde se movía al caer).
[RequireComponent(typeof(Collider2D))]
public class WaterHazard : MonoBehaviour
{
    [Tooltip("Orilla izquierda (o la única orilla)")]
    [SerializeField] private Transform respawnPoint;
    [Tooltip("Opcional: orilla derecha. Si cae yendo hacia la izquierda, reaparece aquí")]
    [SerializeField] private Transform respawnPointDerecho;
    [SerializeField] private float damage = 20f;
    [SerializeField] private AudioClip splashSound;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        SoundFX.Play(splashSound);

        // Primero se reubica: si el daño lo mata, el objeto se destruye
        Rigidbody2D rb = other.attachedRigidbody;
        Transform punto = respawnPoint;
        // Iba hacia la izquierda: saltó desde la orilla derecha
        if (respawnPointDerecho != null && rb != null && rb.linearVelocity.x < -0.1f) punto = respawnPointDerecho;
        if (punto != null)
        {
            if (rb != null)
            {
                rb.position = punto.position;
                rb.linearVelocity = Vector2.zero;
            }
            else
            {
                other.transform.position = punto.position;
            }
        }

        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null) health.TakeDamage(damage);
    }

    private void OnDrawGizmos()
    {
        if (respawnPoint == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(respawnPoint.position, 0.4f);
        Gizmos.DrawLine(transform.position, respawnPoint.position);
    }
}
