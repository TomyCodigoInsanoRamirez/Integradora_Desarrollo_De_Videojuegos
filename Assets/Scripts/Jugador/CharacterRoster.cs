using UnityEngine;

// Lista de personajes jugables: la usan el menú de selección y el PlayerSpawner del nivel
[CreateAssetMenu(fileName = "CharacterRoster", menuName = "Juego/Lista de personajes")]
public class CharacterRoster : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string displayName;
        public GameObject prefab;
        public Sprite preview;
    }

    public Entry[] characters;
}

// Recuerda el personaje elegido al cambiar de escena (-1 = ninguno, se usa el Player de la escena)
public static class CharacterSelection
{
    public static int SelectedIndex = -1;
}
