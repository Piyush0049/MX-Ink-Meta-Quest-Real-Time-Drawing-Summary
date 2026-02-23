using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Two-stage Hugging Face Inference API pipeline (uses new router.huggingface.co):
///
///   Stage 1 — Vision (Qwen2.5-VL):
///     POST chat completion with base64 PNG → router.huggingface.co/v1/chat/completions
///     → returns OpenAI-style: {"choices":[{"message":{"content":"..."}}]}
///
///   Stage 2 — Summarize (BART):
///     POST {"inputs":"..."} → router.huggingface.co/hf-inference/models/facebook/bart-large-cnn
///     → returns: [{"summary_text": "concise AI summary ..."}]
///
/// Both use the FREE HuggingFace token (some free credits included).
/// Get your token at: https://huggingface.co/settings/tokens
/// Put it in: Assets/Resources/ApiConfig.json → "hf_api_token"
/// </summary>
[RequireComponent(typeof(WhiteboardCapture))]
public class AIWhiteboardManager : MonoBehaviour
{
    // ── HuggingFace endpoints (router.huggingface.co — replaces deprecated api-inference) ─
    // Stage 1: vision via Chat Completion API (image-text-to-text VLM)
    private const string HF_VISION_URL   = "https://router.huggingface.co/v1/chat/completions";
    private const string HF_VISION_MODEL = "Qwen/Qwen2.5-VL-7B-Instruct";
    // Stage 2: summarization via hf-inference provider (still works for text tasks)
    private const string HF_SUMMARY_URL  = "https://router.huggingface.co/hf-inference/models/facebook/bart-large-cnn";

    [Header("References")]
    public WhiteboardUI whiteboardUI;

    // ── private ────────────────────────────────────────────────────────────
    private WhiteboardCapture _capture;
    private string            _hfToken;
    private bool              _requesting;

    void Awake()
    {
        _capture = GetComponent<WhiteboardCapture>();
        LoadToken();
    }

    // ── public entry point ─────────────────────────────────────────────────

    /// <summary>Called by the UI "AI Summarize" button.</summary>
    public void RequestSummary()
    {
        if (_requesting)
        {
            whiteboardUI?.ShowStatus("Already processing — please wait...");
            return;
        }
        if (string.IsNullOrEmpty(_hfToken))
        {
            whiteboardUI?.ShowStatus("ERROR: No HF token found in ApiConfig.json");
            return;
        }
        StartCoroutine(PipelineCoroutine());
    }

    // ── main pipeline coroutine ────────────────────────────────────────────

    IEnumerator PipelineCoroutine()
    {
        _requesting = true;

        // ── Stage 1: Capture whiteboard ────────────────────────────────────
        whiteboardUI?.ShowStatus("Capturing whiteboard...");
        yield return null;

        byte[] pngBytes = _capture.CaptureToPngBytes();

        // ── Stage 2: BLIP image → caption ─────────────────────────────────
        whiteboardUI?.ShowStatus("Stage 1/2 — Describing drawing (BLIP)...");

        string caption = null;
        yield return StartCoroutine(CallVisionModel(pngBytes, result => caption = result));

        if (caption == null)
        {
            _requesting = false;
            yield break; // error already shown
        }

        Debug.Log($"[HF BLIP] Caption: {caption}");

        // ── Stage 3: BART caption → summary ───────────────────────────────
        whiteboardUI?.ShowStatus("Stage 2/2 — Generating AI summary (BART)...");

        string summary = null;
        yield return StartCoroutine(CallSummaryModel(caption, result => summary = result));

        _requesting = false;

        if (summary != null)
            whiteboardUI?.ShowSummary(summary);
    }

    // ── Stage 1: Vision (Qwen2.5-VL via Chat Completion API) ─────────────────
    // HF deprecated the old image-to-text pipeline (BLIP returns 410 Gone).
    // New approach: send base64 PNG inside an OpenAI-compatible chat completion.

    IEnumerator CallVisionModel(byte[] pngBytes, System.Action<string> onResult)
    {
        string b64    = System.Convert.ToBase64String(pngBytes);
        string dataUri = "data:image/png;base64," + b64;

        // OpenAI-compatible multimodal chat completion body
        string jsonBody =
            "{\"model\":\"" + HF_VISION_MODEL + "\"," +
            "\"messages\":[{\"role\":\"user\",\"content\":[" +
                "{\"type\":\"image_url\",\"image_url\":{\"url\":\"" + dataUri + "\"}}," +
                "{\"type\":\"text\",\"text\":\"Describe what is written or drawn on this whiteboard. Be specific and concise.\"}" +
            "]}]," +
            "\"max_tokens\":200}";

        byte[] body = Encoding.UTF8.GetBytes(jsonBody);

        using var req = new UnityWebRequest(HF_VISION_URL, "POST");
        req.uploadHandler   = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", "Bearer " + _hfToken);
        req.SetRequestHeader("Content-Type",  "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            HandleError("Vision (Qwen)", req);
            onResult(null);
            yield break;
        }

        // Response: {"choices":[{"message":{"role":"assistant","content":"..."}}]}
        string caption = ParseArrayField(req.downloadHandler.text, "content");
        if (string.IsNullOrEmpty(caption))
        {
            whiteboardUI?.ShowStatus("Vision: no caption returned. Try drawing more.");
            onResult(null);
            yield break;
        }

        onResult(caption);
    }

    // ── Stage 2: Summarize (BART) ──────────────────────────────────────────
    // Sends JSON {"inputs": caption}; HF returns [{"summary_text":"..."}]

    IEnumerator CallSummaryModel(string caption, System.Action<string> onResult)
    {
        // Prefix to help BART produce a coherent summary
        string prompt = $"Whiteboard content: {caption}. " +
                        "This whiteboard shows notes or diagrams. " +
                        "Summarize the key points clearly and concisely.";

        string json = $"{{\"inputs\":\"{EscapeJson(prompt)}\"," +
                       "\"parameters\":{\"max_length\":120,\"min_length\":30}}";

        byte[] body = Encoding.UTF8.GetBytes(json);

        using var req = new UnityWebRequest(HF_SUMMARY_URL, "POST");
        req.uploadHandler   = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", "Bearer " + _hfToken);
        req.SetRequestHeader("Content-Type",  "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            // BART can fail if input is too short — fall back to raw caption
            Debug.LogWarning($"[HF BART] {req.error}. Falling back to raw caption.");
            onResult("Whiteboard shows: " + caption);
            yield break;
        }

        // Response: [{"summary_text":"..."}]
        string summary = ParseArrayField(req.downloadHandler.text, "summary_text");
        onResult(string.IsNullOrEmpty(summary) ? caption : summary);
    }

    // ── helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the first occurrence of "fieldName":"value" from a JSON array response.
    /// Handles both  [{"field":"val"}]  and  {"field":"val"} shapes.
    /// </summary>
    string ParseArrayField(string json, string fieldName)
    {
        string marker = $"\"{fieldName}\":\"";
        int start = json.IndexOf(marker);
        if (start < 0) return null;
        start += marker.Length;
        int end = json.IndexOf("\"", start);
        if (end < 0) return null;
        return json.Substring(start, end - start)
                   .Replace("\\n", "\n")
                   .Replace("\\\"", "\"")
                   .Replace("\\\\", "\\");
    }

    string EscapeJson(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

    void HandleError(string stage, UnityWebRequest req)
    {
        string body = req.downloadHandler?.text ?? "";
        Debug.LogWarning($"[HF {stage}] {req.error}\n{body}");

        // HF returns 503 while model is loading on cold start
        string msg = req.responseCode == 503
            ? $"{stage}: Model loading (HF cold start). Wait 20s & retry."
            : $"{stage} error: {req.error}";

        whiteboardUI?.ShowStatus(msg);
    }

    // ── config ─────────────────────────────────────────────────────────────

    [System.Serializable]
    private class ApiConfig { public string hf_api_token; }

    void LoadToken()
    {
        string json = null;

        // Primary: asset database (works in builds)
        var cfg = Resources.Load<TextAsset>("ApiConfig");
        if (cfg != null) json = cfg.text;

#if UNITY_EDITOR
        // Editor fallback: read from disk directly.
        // Needed because AssetDatabase.Refresh() called by EnvToConfig may not
        // have completed by the time Awake() runs, so Resources.Load returns null.
        if (string.IsNullOrWhiteSpace(json))
        {
            string path = System.IO.Path.Combine(
                Application.dataPath, "Resources", "ApiConfig.json");
            if (System.IO.File.Exists(path))
            {
                json = System.IO.File.ReadAllText(path);
                Debug.Log("[HF] Loaded ApiConfig.json directly from disk (asset DB not ready yet).");
            }
        }
#endif

        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogError("[HF] ApiConfig.json not found or empty. " +
                           "Make sure Assets/Resources/ApiConfig.json exists and your .env is set.");
            return;
        }

        var config = JsonUtility.FromJson<ApiConfig>(json);
        _hfToken   = config?.hf_api_token?.Trim();

        if (string.IsNullOrEmpty(_hfToken))
        {
            Debug.LogError($"[HF] 'hf_api_token' missing or empty in ApiConfig.json. Content: {json}");
            return;
        }

        Debug.Log($"[HF] Token loaded OK (starts with: {_hfToken[..8]}...)");
    }
}
