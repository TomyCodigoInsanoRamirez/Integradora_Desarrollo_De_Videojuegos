using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifetime = 3f;

    private Vector2 direction = Vector2.right;

    public void SetDirection(Vector2 newDirection)
    {
        direction = newDirection.normalized;
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }
}
