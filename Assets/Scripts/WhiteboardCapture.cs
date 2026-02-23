using System;
using UnityEngine;

/// <summary>
/// Renders the current whiteboard strokes to a Texture2D.
///
/// Exposes two methods:
///   CaptureToPngBytes() → raw byte[]  (used by HF Inference API, Content-Type: octet-stream)
///   CaptureToBase64()   → base64 string  (kept for any other integrations)
///
/// How it works:
///   1. Creates a temporary RenderTexture with a plain white background.
///   2. Moves a capture camera to frame the drawing area.
///   3. Renders one frame to the RT.
///   4. Reads pixels back to CPU → converts to PNG.
/// </summary>
[RequireComponent(typeof(AirDraw))]
public class WhiteboardCapture : MonoBehaviour
{
    [Header("Capture Camera")]
    [Tooltip("An orthographic camera used only for capture. Leave blank to auto-create.")]
    public Camera captureCamera;

    [Header("Output Size")]
    public int captureWidth  = 1024;
    public int captureHeight = 768;

    // ── internal ───────────────────────────────────────────────────────────
    private AirDraw _airDraw;

    void Awake()
    {
        _airDraw = GetComponent<AirDraw>();

        if (captureCamera == null)
            captureCamera = CreateCaptureCamera();
    }

    // ── public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns raw PNG bytes of the current whiteboard.
    /// Required by Hugging Face Inference API (Content-Type: application/octet-stream).
    /// </summary>
    public byte[] CaptureToPngBytes()
    {
        byte[] png = RenderToTexture(out var tex);
        Destroy(tex);
        return png;
    }

    /// <summary>
    /// Returns a base-64 encoded PNG string (kept as utility method).
    /// </summary>
    public string CaptureToBase64()
    {
        byte[] png = RenderToTexture(out var tex);
        Destroy(tex);
        return Convert.ToBase64String(png);
    }

    // ── shared render logic ────────────────────────────────────────────────

    byte[] RenderToTexture(out Texture2D tex)
    {
        FrameCamera();

        var rt = new RenderTexture(captureWidth, captureHeight, 24);
        captureCamera.targetTexture   = rt;
        captureCamera.backgroundColor = Color.white;
        captureCamera.clearFlags      = CameraClearFlags.SolidColor;
        captureCamera.Render();

        RenderTexture.active = rt;
        tex = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        tex.Apply();

        captureCamera.targetTexture = null;
        RenderTexture.active        = null;
        Destroy(rt);

        return tex.EncodeToPNG();
    }

    // ── helpers ────────────────────────────────────────────────────────────

    void FrameCamera()
    {
        // Use the main camera's position/direction so the capture matches the view
        if (Camera.main != null)
        {
            captureCamera.transform.position = Camera.main.transform.position;
            captureCamera.transform.rotation = Camera.main.transform.rotation;
            captureCamera.fieldOfView        = Camera.main.fieldOfView;
        }
    }

    Camera CreateCaptureCamera()
    {
        var go = new GameObject("_WhiteboardCaptureCamera");
        go.hideFlags = HideFlags.HideAndDontSave;
        var cam = go.AddComponent<Camera>();
        cam.enabled = false; // only render on demand
        return cam;
    }
}
