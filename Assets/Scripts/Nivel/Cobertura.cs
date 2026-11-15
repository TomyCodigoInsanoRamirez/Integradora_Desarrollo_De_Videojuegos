using UnityEngine;

// Cobertura: detiene las balas enemigas (el jugador se agacha o se para detrás para protegerse).
// El jugador la atraviesa: su collider es un trigger que solo usan las balas.
[RequireComponent(typeof(Collider2D))]
public class Cobertura : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }
}
