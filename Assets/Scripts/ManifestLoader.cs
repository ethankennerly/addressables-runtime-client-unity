using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public static class ManifestLoader
{
    public static async Task<string> LoadManifestJsonAsync(string basePath)
    {
#if UNITY_EDITOR
        if (!IsHttp(basePath))
        {
            var path = Path.Combine(basePath, "packs.json");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"packs.json not found at {path}");
            }
            return await Task.Run(() => File.ReadAllText(path));
        }
#endif
        if (!IsHttps(basePath))
        {
            throw new System.Exception("Mobile requires HTTPS. Update BaseUrlOrPath.");
        }
        var url = Join(basePath, "packs.json");
        using var req = UnityEngine.Networking.UnityWebRequest.Get(url);
        var op = req.SendWebRequest();
        while (!op.isDone)
        {
            await Task.Yield();
        }
        if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            throw new System.Exception($"Could not fetch packs.json: {req.error}");
        }
        return req.downloadHandler.text;
    }

    private static bool IsHttp(string s)
    {
        return s.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHttps(string s)
    {
        return s.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase);
    }

    private static string Join(string basePath, string file)
    {
        return IsHttp(basePath) ? (basePath.EndsWith("/") ? basePath + file : basePath + "/" + file) : Path.Combine(basePath, file);
    }
}
