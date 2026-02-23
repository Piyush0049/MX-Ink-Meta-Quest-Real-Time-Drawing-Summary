# MX Ink Whiteboard

> **AI-Powered Mixed Reality Smart Whiteboard** — draw in the air, let AI read and summarize your notes, and see the result rendered live on a Mixed Reality panel.

&copy; 2026 piyush0049 — All Rights Reserved. See [LICENSE](LICENSE) for terms.

---

## Overview

MX Ink Whiteboard is a Mixed Reality application built with Unity that turns freehand air-drawing into structured, AI-generated summaries. The user draws strokes in world space using the Logitech MX Ink stylus (or mouse in simulator mode). On demand, the whiteboard is captured as an image and sent through a two-stage Hugging Face Inference pipeline: first a vision model reads the content, then a language model condenses it into clean notes — displayed with a typewriter animation on a floating MR panel.

### AI Pipeline

| Stage | Model | Role |
|-------|-------|------|
| 1 — Vision | `Salesforce/blip-image-captioning-large` | Reads the whiteboard image and generates a description |
| 2 — Language | `facebook/bart-large-cnn` | Summarizes the description into structured notes |

Both models are accessed via the **Hugging Face Inference API** (free tier).

---

## Tech Stack

- **Engine** — Unity 2022.3 LTS
- **XR** — OpenXR + XR Interaction Toolkit
- **AI** — Hugging Face Inference API (BLIP + BART)
- **Rendering** — LineRenderer strokes on RenderTexture → PNG capture
- **UI** — TextMeshPro World-Space Canvas with typewriter animation

---

## Prerequisites

- Windows 10 / 11 (64-bit)
- Unity Hub + Unity 2022.3 LTS
- A free [Hugging Face](https://huggingface.co) account

---

## Setup Guide

### 1. Install Unity

1. Download and run the Unity Hub installer from **https://unity.com/download**.
2. Sign in with a Unity account (free Personal license is sufficient).
3. Navigate to **Installs → Install Editor** and select **Unity 2022.3 LTS**.
4. During module selection, enable:
   - **Microsoft Visual Studio Community**
   - **Android Build Support**
   - **Android SDK & NDK Tools**
   - **OpenJDK**
5. Click **Install** and wait for completion (~20–40 min).

---

### 2. Open the Project

1. In Unity Hub → **Projects → Open**.
2. Select the `mx_ink/` folder.
3. Unity will automatically detect and open it as a Unity project.
4. The first open may take 5–10 minutes for package import and shader compilation.
5. If prompted about API updates, choose **"Yes, for these and other files"**.

---

### 3. Configure the Hugging Face API Token

1. Sign up at **https://huggingface.co** and verify your email.
2. Go to **Settings → Access Tokens → New token**.
   - Name: `unity-whiteboard`
   - Role: **Read**
3. Copy the generated token (it begins with `hf_`).
4. Open `Assets/Resources/ApiConfig.json` and insert your token:

```json
{
  "hf_api_token": "hf_YOUR_TOKEN_GOES_HERE"
}
```

> **Security note:** `ApiConfig.json` is listed in `.gitignore` and will never be committed to source control.

---

### 4. Scene Setup

#### A — XR Plugin Management
1. **Edit → Project Settings → XR Plugin Management** → Install.
2. Enable **OpenXR**.
3. Under **OpenXR**, add the **Mock HMD** feature (enables testing without a physical headset).

#### B — DrawingManager GameObject
1. Right-click Hierarchy → **Create Empty** → rename to `DrawingManager`.
2. Add the following components via **Inspector → Add Component**:
   - `AirDraw`
   - `WhiteboardCapture`
   - `AIWhiteboardManager`

#### C — Line Prefab
1. Right-click Hierarchy → **Create Empty** → rename to `LinePrefab`.
2. Add Component → **Line Renderer**:
   - Width Start/End: `0.01`
   - Use World Space: enabled
3. Create a new Material at `Assets/Materials/` → name it `InkMat` → set color to bright blue.
4. Assign `InkMat` to the Line Renderer's material slot.
5. Drag `LinePrefab` from the Hierarchy into `Assets/Prefabs/`, then **delete** it from the Hierarchy.
6. On `DrawingManager → AirDraw`, assign the `LinePrefab` prefab to the **Line Prefab** slot.

#### D — XR Simulator
1. Right-click Hierarchy → **XR → XR Origin (VR)**.
2. Right-click Hierarchy → **XR → Device Simulator**.

#### E — MR Summary Panel
1. Right-click Hierarchy → **UI → Canvas**:
   - Render Mode: **World Space**
   - Size: `800 × 600`
   - Position: `(0, 0, 2)`
   - Scale: `(0.005, 0.005, 0.005)`
2. Add a **UI → Panel** child (background alpha: `0.8`).
3. Add a **UI → Text - TextMeshPro** → rename `StatusText` (anchor: top, size: 18).
4. Add a **UI → Text - TextMeshPro** → rename `SummaryText` (anchor: middle, size: 22, auto-size enabled).
5. Add a **UI → Button - TextMeshPro** → rename `SummarizeBtn` (label: `AI Summarize`).
6. Add a **UI → Button - TextMeshPro** → rename `ClearBtn` (label: `Clear`).
7. Select Canvas → Add Component → `WhiteboardUI` → wire all six fields in the Inspector.

#### F — Wire AIWhiteboardManager
Select `DrawingManager` → in the `AIWhiteboardManager` component, assign the **Whiteboard UI** field to the Canvas object.

---

## Controls (XR Device Simulator)

| Action | Input |
|--------|-------|
| Draw stroke | Hold **Left Mouse Button** and move the mouse |
| Stop drawing | Release the mouse button |
| Move camera | **W A S D** |
| Look around | **Right Mouse Button** + drag |
| Trigger AI summary | Click **AI Summarize** in the scene UI |
| Clear the canvas | Click **Clear** in the scene UI |

---

## Architecture

```
User Input (stylus / mouse)
        │
        ▼
   AirDraw.cs
   Captures pointer position and instantiates
   LineRenderer strokes in world space.
        │
        ▼
   WhiteboardCapture.cs
   Renders all strokes to a RenderTexture,
   composites over a white background, and
   exports raw PNG bytes.
        │
        ▼
   AIWhiteboardManager.cs
        │
        ├─► Stage 1 — Hugging Face Inference API
        │     Model : Salesforce/blip-image-captioning-large
        │     Input : PNG bytes (octet-stream)
        │     Output: "a whiteboard showing …"
        │
        └─► Stage 2 — Hugging Face Inference API
              Model : facebook/bart-large-cnn
              Input : BLIP caption as plain text
              Output: Concise bullet-point summary
                      │
                      ▼
               WhiteboardUI.cs
               Animates summary onto the
               MR panel using a typewriter effect.
```

---

## Project Structure

```
mx_ink/
├── Assets/
│   ├── Scripts/
│   │   ├── AirDraw.cs              # Stroke input and LineRenderer management
│   │   ├── WhiteboardCapture.cs    # RenderTexture → PNG byte array
│   │   ├── AIWhiteboardManager.cs  # Hugging Face API orchestration (BLIP + BART)
│   │   └── WhiteboardUI.cs         # MR panel UI and typewriter animation
│   ├── Resources/
│   │   └── ApiConfig.json          # API token (git-ignored, never committed)
│   ├── Materials/                  # InkMat material
│   └── Prefabs/                    # LinePrefab
├── Packages/
│   └── manifest.json               # XR Toolkit, OpenXR, TMP (auto-resolved)
├── .gitignore
├── LICENSE
└── README.md
```

---

## Troubleshooting

| Symptom | Resolution |
|---------|------------|
| HTTP 503 — "Model loading" | The Hugging Face free tier cold-starts models. Wait ~20 seconds and retry. |
| Ink strokes not visible | Confirm `LinePrefab` is assigned in the `AirDraw` component inspector. |
| "No HF token" error in console | Verify `ApiConfig.json` contains the `hf_api_token` key with no surrounding spaces. |
| BART returns an empty summary | The BLIP caption may be too short (<30 characters). The BLIP caption is displayed as a fallback. Try drawing more content. |
| `TMP` namespace errors on compile | **Window → Package Manager** → locate **TextMeshPro** → Install. |

---

## License

This project is proprietary. Copying, redistribution, or modification of any kind is strictly prohibited without explicit written permission from the author. See [LICENSE](LICENSE) for full terms.
