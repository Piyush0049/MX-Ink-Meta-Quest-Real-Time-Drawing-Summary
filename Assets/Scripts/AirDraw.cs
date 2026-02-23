using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles air drawing with mouse (XR Device Simulator) or XR stylus input.
/// Attach to a DrawingManager GameObject. Assign linePrefab in Inspector.
/// </summary>
public class AirDraw : MonoBehaviour
{
    [Header("Line Settings")]
    public LineRenderer linePrefab;
    public float lineWidth = 0.01f;
    public Color inkColor = Color.blue;

    [Header("Draw Plane")]
    [Tooltip("Distance from camera at which strokes are placed")]
    public float drawDepth = 2.0f;

    [Header("References")]
    public WhiteboardUI ui;

    // ── state ──────────────────────────────────────────────────────────────
    private LineRenderer    _currentLine;
    private bool            _isDrawing;
    private List<Vector3>   _currentPoints = new();
    private List<LineRenderer> _allStrokes = new();

    void Update()
    {
        // Works with XR Device Simulator (mouse) AND real XR controller trigger
        bool startDraw  = Input.GetMouseButtonDown(0);
        bool continueDraw = Input.GetMouseButton(0);
        bool endDraw    = Input.GetMouseButtonUp(0);

        if (startDraw)   BeginStroke();
        if (continueDraw && _isDrawing) ContinueStroke();
        if (endDraw)     EndStroke();
    }

    // ── drawing lifecycle ──────────────────────────────────────────────────

    void BeginStroke()
    {
        _currentLine = Instantiate(linePrefab, transform);
        _currentLine.startWidth  = lineWidth;
        _currentLine.endWidth    = lineWidth;
        _currentLine.startColor  = inkColor;
        _currentLine.endColor    = inkColor;
        _currentLine.positionCount = 0;
        _currentPoints.Clear();
        _isDrawing = true;
    }

    void ContinueStroke()
    {
        Vector3 pos = GetWorldPosition();

        // Skip duplicate or near-duplicate points to keep renderer tidy
        if (_currentPoints.Count > 0 &&
            Vector3.Distance(_currentPoints[^1], pos) < 0.002f)
            return;

        _currentPoints.Add(pos);
        _currentLine.positionCount = _currentPoints.Count;
        _currentLine.SetPosition(_currentPoints.Count - 1, pos);
    }

    void EndStroke()
    {
        _isDrawing = false;
        if (_currentLine != null && _currentLine.positionCount > 1)
            _allStrokes.Add(_currentLine);
        _currentLine = null;
    }

    // ── helpers ────────────────────────────────────────────────────────────

    Vector3 GetWorldPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        return ray.origin + ray.direction * drawDepth;
    }

    /// <summary>Erase all strokes (called by UI clear button).</summary>
    public void ClearAll()
    {
        foreach (var s in _allStrokes)
            if (s != null) Destroy(s.gameObject);
        _allStrokes.Clear();
        if (_currentLine != null) Destroy(_currentLine.gameObject);
        _currentLine = null;
        _isDrawing = false;
    }

    /// <summary>Returns all active stroke renderers (used by WhiteboardCapture).</summary>
    public IReadOnlyList<LineRenderer> GetStrokes() => _allStrokes;
}
