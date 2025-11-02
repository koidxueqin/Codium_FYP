using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class UISyncedSnapZone : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private RectTransform uiArea;

    Collider2D zoneCollider;

    void Awake()
    {
        zoneCollider = GetComponent<Collider2D>();
    }

    void LateUpdate()
    {
        // 1. Get UI area's screen-space center
        Vector3[] corners = new Vector3[4];
        uiArea.GetWorldCorners(corners);
        Vector3 center = (corners[0] + corners[2]) * 0.5f;

        // 2. Maintain collider’s current depth
        float depth = Vector3.Dot(
            transform.position - targetCamera.transform.position,
            targetCamera.transform.forward
        );

        // 3. Convert to world position
        Vector3 worldPos = targetCamera.ScreenToWorldPoint(new Vector3(center.x, center.y, depth));

        // 4. Move collider to match
        transform.position = worldPos;
    }
}