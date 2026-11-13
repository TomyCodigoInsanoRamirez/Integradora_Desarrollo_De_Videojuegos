using UnityEngine;

// Sustituye el Player de la escena por el personaje elegido en el menú.
// Si se da Play directo en el nivel (sin pasar por el menú), se queda el Player de la escena.
public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private CharacterRoster roster;
    [SerializeField] private GameObject scenePlayer;
    [SerializeField] private CameraFollow cameraFollow;

    private void Awake()
    {
        int index = CharacterSelection.SelectedIndex;
        if (roster == null || scenePlayer == null) return;
        if (index < 0 || index >= roster.characters.Length || roster.characters[index].prefab == null) return;

        GameObject player = Instantiate(roster.characters[index].prefab, scenePlayer.transform.position, Quaternion.identity);
        player.name = "Player";

        // Se desactiva antes de destruirlo para que nadie lo encuentre por tag en su Start
        scenePlayer.SetActive(false);
        Destroy(scenePlayer);

        if (cameraFollow != null) cameraFollow.SetTarget(player.transform);
    }
}
