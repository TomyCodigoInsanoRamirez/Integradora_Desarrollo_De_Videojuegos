using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    private float health;

    private void Awake()
    {
        health = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        health -= amount;

        if (health <= 0f)
        {
            health = 0f;
            Debug.Log("El jugador ha muerto");
            Destroy(gameObject);
        }
    }
}
