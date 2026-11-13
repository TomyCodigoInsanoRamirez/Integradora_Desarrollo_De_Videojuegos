using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Inventario")]
    [SerializeField] private bool hasTubo = false;

    public bool HasTubo => hasTubo;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void AddTubo()
    {
        hasTubo = true;
    }
}
