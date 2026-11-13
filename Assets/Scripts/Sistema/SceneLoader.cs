using UnityEngine.SceneManagement;

// Nombres de escenas y carga a través de la pantalla de carga
public static class SceneLoader
{
    public const string MainMenu = "MenuPrincipal";
    public const string CharacterSelect = "SeleccionPersonaje";
    public const string Cinematic = "Cinematica";
    public const string Loading = "Carga";
    public const string Game = "Nivel1";
    public const string TestScene = "EscenaDePruebas";

    // Escena que la pantalla de carga debe abrir
    public static string PendingScene { get; private set; } = Game;

    public static void LoadWithLoadingScreen(string sceneName)
    {
        PendingScene = sceneName;
        SceneManager.LoadScene(Loading);
    }
}
