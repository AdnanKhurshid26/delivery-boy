using UnityEngine;

// Slaves a touchpoint widget's visibility to the screen panel it belongs to. Added at runtime
// to the panel by PlaySuperWidgetMount - no polling, and no scene or prefab edit.
[DisallowMultipleComponent]
public class PlaySuperScreenVisibilityBinder : MonoBehaviour
{
    [SerializeField] GameObject widget;

    public GameObject Widget { get { return widget; } }

    public void Bind(GameObject value)
    {
        widget = value;
        if (widget != null) widget.SetActive(gameObject.activeInHierarchy);
    }

    void OnEnable()
    {
        if (widget != null) widget.SetActive(true);
    }

    void OnDisable()
    {
        if (widget != null) widget.SetActive(false);
    }

    void OnDestroy()
    {
        if (widget != null) Destroy(widget);
    }
}
