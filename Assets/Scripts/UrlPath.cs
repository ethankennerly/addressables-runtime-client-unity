using System;
using System.IO;
using UnityEngine;

public static class UrlPath
{
    public static bool IsHttp(string s)
    {
        return s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsHttps(string s)
    {
        return s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    public static string Join(string basePath, string file)
    {
        if (IsHttp(basePath))
        {
            return basePath.EndsWith("/") ? basePath + file : basePath + "/" + file;
        }
        return Path.Combine(basePath, file);
    }

    // If 'path' is relative and not http(s), resolve to absolute using project root
    public static string ResolveProjectRelative(string path)
    {
        if (IsHttp(path) || Path.IsPathRooted(path))
        {
            return path;
        }
        // Application.dataPath -> <project>/Assets; go up one
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, path));
    }
}
