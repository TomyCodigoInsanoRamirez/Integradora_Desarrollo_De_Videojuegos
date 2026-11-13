using UnityEngine;

// Efecto de explosión hecho con sistemas de partículas: los reproduce todos al aparecer
// y se destruye solo cuando el último terminó.
public class ExplosionEffect : MonoBehaviour
{
    private void Start()
    {
        float longest = 0f;
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>())
        {
            ps.Play(false);
            var main = ps.main;
            longest = Mathf.Max(longest, main.duration + main.startLifetime.constantMax);
        }
        Destroy(gameObject, longest + 0.2f);
    }
}
