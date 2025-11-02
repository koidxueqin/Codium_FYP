using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class DropSnap : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private RectTransform uiArea;
    [SerializeField] private string droppableTag = "Droppable";

    BoxCollider2D zoneCollider;

    void Awake()
    {
        zoneCollider = GetComponent<BoxCollider2D>();
        zoneCollider.isTrigger = true;
    }

    void LateUpdate()
    {
        // get ui corners in screen space
        Vector3[] corners = new Vector3[4];
        uiArea.GetWorldCorners(corners);

        // get center and size in world space
        Vector3 worldBL = targetCamera.ScreenToWorldPoint(corners[0]);
        Vector3 worldTR = targetCamera.ScreenToWorldPoint(corners[2]);

        Vector2 worldCenter = (Vector2)((worldBL + worldTR) * 0.5f);
        Vector2 worldSize = worldTR - worldBL;

        // update collider position and size
        transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);
        zoneCollider.size = worldSize;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(droppableTag)) return;

        // snap to ui area's world center, keep original z
        Vector3[] corners = new Vector3[4];
        uiArea.GetWorldCorners(corners);
        Vector3 centerScreen = (corners[0] + corners[2]) * 0.5f;
        Vector3 worldCenter = targetCamera.ScreenToWorldPoint(centerScreen);
        worldCenter.z = other.transform.position.z;

        other.transform.position = worldCenter;
    }
}