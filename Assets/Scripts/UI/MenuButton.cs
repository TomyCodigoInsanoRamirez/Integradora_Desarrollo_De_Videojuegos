using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Botón del menú: al pasar el mouse se selecciona (así mouse y teclado comparten un solo resaltado)
// y el botón seleccionado crece un poco.
[RequireComponent(typeof(Button))]
public class MenuButton : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private float selectedScale = 1.06f;
    [SerializeField] private float speed = 14f;

    private bool selected;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current != null && GetComponent<Button>().interactable)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnSelect(BaseEventData eventData) => selected = true;
    public void OnDeselect(BaseEventData eventData) => selected = false;

    private void OnDisable()
    {
        selected = false;
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        float target = selected ? selectedScale : 1f;
        float k = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, k);
    }
}
