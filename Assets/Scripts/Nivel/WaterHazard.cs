using UnityEngine;

// Agua profunda: si el jugador cae, pierde vida y reaparece en la orilla.
[RequireComponent(typeof(Collider2D))]
public class WaterHazard : MonoBehaviour
{
    [SerializeField] private Transform respawnPoint;
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
        if (respawnPoint != null)
        {
            if (rb != null)
            {
                rb.position = respawnPoint.position;
                rb.linearVelocity = Vector2.zero;
            }
            else
            {
                other.transform.position = respawnPoint.position;
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
