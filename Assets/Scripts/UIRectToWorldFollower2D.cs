// UIRectToWorldFollower2D.cs
using UnityEngine;

[ExecuteAlways]
public class UIRectToWorldFollower2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform uiArea;   // the UI blank area between prefix/suffix
    [SerializeField] private Camera targetCamera;    // Canvas Render Camera if Screen Space - Camera; else main
    [SerializeField] private CheckpointSlot2D slot;  // slot_0 (optional; used to sync snapPoint)

    [Header("Behavior")]
    [SerializeField] private bool followContinuously = true; // true = update in LateUpdate
    [SerializeField] private Vector3 worldOffset = Vector3.zero;
    [SerializeField] private bool syncSnapPoint = true;
    [SerializeField] private bool keepCurrentZ = true;       // keep existing Z depth

    void Reset()
    {
        slot = GetComponent<CheckpointSlot2D>();
        if (!targetCamera) targetCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (!followContinuously) return;
        SnapNow();
    }

    public void SnapNow()
    {
        if (!uiArea) return;

        var canvas = uiArea.GetComponentInParent<Canvas>();
        if (!canvas)
            return;

        // 1) Get UI rect center in screen space
        var corners = new Vector3[4];
        uiArea.GetWorldCorners(corners); // world positions of rect corners
        // Convert to screen coords to be robust across Canvas modes
        Vector3 bl = WorldToScreen(corners[0], canvas);
        Vector3 tr = WorldToScreen(corners[2], canvas);
        Vector3 screenCenter = (bl + tr) * 0.5f;

        // 2) Convert screen -> world using camera depth aligned with current transform
        Camera cam = ResolveCamera(canvas);
        if (!cam) cam = Camera.main;

        float zDepth = GetCameraDepthForThisTransform(cam);
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenCenter.x, screenCenter.y, zDepth));

        if (keepCurrentZ)
            world.z = transform.position.z; // keep existing z plane

        // 3) Apply offset and move
        world += worldOffset;
        transform.position = world;

        // 4) Keep snapPoint centered on slot (optional)
        if (syncSnapPoint && slot && slot.snapPoint)
        {
            slot.snapPoint.position = world;
            slot.snapPoint.rotation = transform.rotation;
        }
    }

    Camera ResolveCamera(Canvas canvas)
    {
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return targetCamera ? targetCamera : Camera.main;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            return canvas.worldCamera ? canvas.worldCamera : targetCamera;
        // World Space canvas: camera not needed for conversion, but we still use main for consistency
        return targetCamera ? targetCamera : Camera.main;
    }

    Vector3 WorldToScreen(Vector3 worldPos, Canvas canvas)
    {
        var cam = ResolveCamera(canvas);
        // For Overlay mode, cam can be null; RectTransformUtility handles null = Overlay
        return RectTransformUtility.WorldToScreenPoint(cam, worldPos);
    }

    float GetCameraDepthForThisTransform(Camera cam)
    {
        // Distance from camera to current object along camera forward
        Vector3 toObj = transform.position - cam.transform.position;
        return Mathf.Abs(Vector3.Dot(toObj, cam.transform.forward));
    }
}
