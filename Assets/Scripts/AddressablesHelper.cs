using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

public static class AddressablesHelper
{
    public static List<string> GetAddressesByPrefix(string prefix)
    {
        prefix = prefix.ToLowerInvariant();
        var set = new HashSet<string>();
        foreach (var loc in Addressables.ResourceLocators)
        {
            foreach (var key in loc.Keys)
            {
                if (key is string s && s.ToLowerInvariant().StartsWith(prefix))
                {
                    set.Add(s);
                }
            }
        }
        return set.OrderBy(s => s).ToList();
    }

    public static async Task<GameObject> InstantiateAsync(string address, List<GameObject> spawned)
    {
        var handle = Addressables.LoadAssetAsync<GameObject>(address);
        while (!handle.IsDone)
        {
            await Task.Yield();
        }
        if (!handle.IsValid() || handle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            Debug.LogWarning($"[AddressablesHelper] Load failed: {address}");
            return null;
        }
        var go = Object.Instantiate(handle.Result, Vector3.zero, Quaternion.identity);
        spawned.Add(go);
        Addressables.Release(handle);
        return go;
    }
}
