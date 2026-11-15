using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private bool isEnemyBullet = false;
    [Tooltip("Se elige uno al azar cuando una bala del jugador impacta a un enemigo")]
    [SerializeField] private AudioClip[] enemyImpactSounds;

    private Vector2 direction = Vector2.right;

    public void SetDirection(Vector2 newDirection)
    {
        direction = newDirection.normalized;

        // El sprite de la bala apunta a la derecha; se gira para que mire hacia donde viaja
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }

    public void SetDamage(float newDamage)
    {
        damage = newDamage;
    }

    public void SetIsEnemyBullet(bool value)
    {
        isEnemyBullet = value;
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isEnemyBullet)
        {
            // Las coberturas frenan las balas enemigas
            if (other.GetComponent<Cobertura>() != null) { Destroy(gameObject); return; }

            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth == null) return;

            playerHealth.TakeDamage(damage);
        }
        else
        {
            IDamageable enemy = other.GetComponent<IDamageable>();
            if (enemy == null) return;

            SoundFX.PlayRandom(enemyImpactSounds);
            enemy.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}
