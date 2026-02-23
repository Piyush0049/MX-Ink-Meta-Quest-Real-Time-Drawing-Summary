using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the in-world MR panel that shows:
///   - Status messages ("Capturing…", "Sending to AI…")
///   - The AI summary result
///   - Buttons: [AI Summarize]  [Clear]
///
/// Scene setup (do once in Unity):
///   1. Right-click Hierarchy → UI → Canvas
///      Set Canvas: Render Mode = World Space, scale ~0.005
///      Position it in front of the camera
///   2. Add a Panel child with a semi-transparent dark background
///   3. Add two TextMeshProUGUI:
///      - statusText   (small, top)
///      - summaryText  (larger area, scrollable)
///   4. Add two Button objects:
///      - summarizeBtn → OnClick → AIWhiteboardManager.RequestSummary()
///      - clearBtn     → OnClick → AirDraw.ClearAll()
///   5. Assign all references in this Inspector.
/// </summary>
public class WhiteboardUI : MonoBehaviour
{
    [Header("Text Elements")]
    public TMP_Text statusText;
    public TMP_Text summaryText;

    [Header("Buttons")]
    public Button summarizeBtn;
    public Button clearBtn;

    [Header("Panel")]
    [Tooltip("Root panel GameObject — used to show/hide the AI panel")]
    public GameObject summaryPanel;

    [Header("References")]
    public AIWhiteboardManager aiManager;
    public AirDraw             airDraw;

    void Start()
    {
        // Wire buttons if not set in Inspector
        summarizeBtn?.onClick.AddListener(() => aiManager?.RequestSummary());
        clearBtn?.onClick.AddListener(() =>
        {
            airDraw?.ClearAll();
            ClearSummary();
        });

        if (summaryPanel != null) summaryPanel.SetActive(false);
        ShowStatus("[F] = AI Summarize   [X] = Clear   Hold LMB = Draw");
    }

    // ── keyboard shortcuts (works with XR Device Simulator) ───────────────
    // F = Summarize, X = Clear  (avoids WASD movement conflict)
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
            aiManager?.RequestSummary();

        if (Input.GetKeyDown(KeyCode.X))
        {
            airDraw?.ClearAll();
            ClearSummary();
        }
    }

    // ── public API (called by AIWhiteboardManager) ─────────────────────────

    public void ShowStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
        Debug.Log("[UI] " + message);
    }

    public void ShowSummary(string summary)
    {
        ShowStatus("AI Summary ready!");

        if (summaryPanel != null) summaryPanel.SetActive(true);
        if (summaryText  != null)
        {
            // Animate text in: start empty and set full text
            summaryText.text = "";
            StartCoroutine(TypewriterEffect(summaryText, summary));
        }
    }

    public void ClearSummary()
    {
        if (summaryPanel != null) summaryPanel.SetActive(false);
        if (summaryText  != null) summaryText.text = "";
        ShowStatus("Canvas cleared. Start drawing!");
    }

    // ── typewriter effect ──────────────────────────────────────────────────

    System.Collections.IEnumerator TypewriterEffect(TMP_Text target, string fullText)
    {
        target.text = "";
        foreach (char c in fullText)
        {
            target.text += c;
            yield return new WaitForSeconds(0.015f); // ~66 chars/sec
        }
    }
}
