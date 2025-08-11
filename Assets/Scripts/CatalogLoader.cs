using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets.ResourceLocators;

public static class CatalogLoader
{
    public static async Task<IResourceLocator> LoadCatalogAsync(string baseUrlOrPath, string catalogFile)
    {
        string catFile = catalogFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? Path.ChangeExtension(catalogFile, ".bin")
            : catalogFile;
#if UNITY_EDITOR
        if (!IsHttp(baseUrlOrPath))
        {
            var filePath = Path.Combine(baseUrlOrPath, catFile);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Catalog missing: {filePath}");
            }
            var catOpFile = Addressables.LoadContentCatalogAsync(filePath);
            await Awaiter(catOpFile);
            if (catOpFile.Status != AsyncOperationStatus.Succeeded || catOpFile.Result == null)
            {
                throw new Exception($"Catalog load failed: {filePath}");
            }
            return catOpFile.Result;
        }
#endif
        var url = Join(baseUrlOrPath, catFile);
        var catOp = Addressables.LoadContentCatalogAsync(url);
        await Awaiter(catOp);
        if (catOp.Status != AsyncOperationStatus.Succeeded || catOp.Result == null)
        {
            throw new Exception($"Catalog load failed: {url}");
        }
        return catOp.Result;
    }

    private static async Task Awaiter(AsyncOperationHandle handle)
    {
        while (!handle.IsDone)
        {
            await Task.Yield();
        }
    }

    private static bool IsHttp(string s)
    {
        return s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static string Join(string basePath, string file)
    {
        return IsHttp(basePath) ? (basePath.EndsWith("/") ? basePath + file : basePath + "/" + file) : Path.Combine(basePath, file);
    }
}
