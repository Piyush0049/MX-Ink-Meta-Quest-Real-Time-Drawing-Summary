using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Runs automatically every time Unity starts or recompiles scripts.
/// Reads HF_API_TOKEN from the project-root .env file and writes it
/// into Assets/Resources/ApiConfig.json — which is gitignored.
///
/// You never edit ApiConfig.json directly; edit .env instead.
/// </summary>
[InitializeOnLoad]
public static class EnvToConfig
{
    static EnvToConfig()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string envPath     = Path.Combine(projectRoot, ".env");

        if (!File.Exists(envPath))
        {
            Debug.LogWarning("[EnvToConfig] .env not found at: " + envPath);
            return;
        }

        string token = null;
        foreach (string line in File.ReadAllLines(envPath))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("#") || !trimmed.Contains("=")) continue;

            int eq  = trimmed.IndexOf('=');
            string key = trimmed[..eq].Trim();
            string val = trimmed[(eq + 1)..].Trim().Trim('"').Trim('\'');

            if (key == "HF_API_TOKEN")
            {
                token = val;
                break;
            }
        }

        if (string.IsNullOrEmpty(token) || token.StartsWith("hf_PASTE"))
        {
            Debug.LogWarning("[EnvToConfig] HF_API_TOKEN not set in .env — paste your token.");
            return;
        }

        // Write ApiConfig.json (gitignored — never committed)
        string configDir  = Path.Combine(Application.dataPath, "Resources");
        string configPath = Path.Combine(configDir, "ApiConfig.json");

        Directory.CreateDirectory(configDir);
        File.WriteAllText(configPath,
            $"{{\n  \"hf_api_token\": \"{token}\"\n}}\n");

        AssetDatabase.Refresh();
        Debug.Log("[EnvToConfig] ApiConfig.json synced from .env ✓");
    }
}
