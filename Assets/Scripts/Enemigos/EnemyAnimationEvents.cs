using UnityEngine;

// Va en el objeto que tiene el Animator del enemigo: los eventos de animación solo llegan
// a ese objeto, así que se reenvían al EnemyRobot del padre
public class EnemyAnimationEvents : MonoBehaviour
{
    private EnemyRobot enemy;

    private void Awake()
    {
        enemy = GetComponentInParent<EnemyRobot>();
    }

    public void DisparoDesdeAnimacion()
    {
        if (enemy != null) enemy.DisparoDesdeAnimacion();
    }
}
