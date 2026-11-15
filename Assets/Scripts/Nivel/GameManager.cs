using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Inventario")]
    [SerializeField] private bool hasTubo = false;

    [Tooltip("Monedas recogidas en el nivel")]
    [SerializeField] private int monedas = 0;

    public bool HasTubo => hasTubo;
    public int Monedas => monedas;

    // Se avisa con el total actual (para la UI o la tienda)
    public event System.Action<int> MonedasCambiaron;

    public void AgregarMonedas(int cantidad)
    {
        monedas += cantidad;
        MonedasCambiaron?.Invoke(monedas);
    }

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
