using UnityEngine;

/// <summary>
/// Keeps the test camera in a blind-perception mode.
/// It hides normal coloured world geometry and shows only the player, UI, and reveal layer.
/// </summary>
[RequireComponent(typeof(Camera))]
public class BlindCameraCullingMask : MonoBehaviour
{
    public string playerLayerName = "Player";
    public string revealLayerName = "PerceptionReveal";
    public bool includeUI = true;
    public Color backgroundColour = Color.black;

    private void Awake()
    {
        ApplyBlindCameraSettings();
    }

    [ContextMenu("Apply Blind Camera Settings")]
    public void ApplyBlindCameraSettings()
    {
        Camera targetCamera = GetComponent<Camera>();
        int mask = 0;

        AddLayerToMask(ref mask, playerLayerName);
        AddLayerToMask(ref mask, revealLayerName);

        if (includeUI)
        {
            AddLayerToMask(ref mask, "UI");
        }

        targetCamera.cullingMask = mask;
        targetCamera.clearFlags = CameraClearFlags.SolidColor;
        targetCamera.backgroundColor = backgroundColour;
    }

    private void AddLayerToMask(ref int mask, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
        {
            mask |= 1 << layer;
        }
    }
}
