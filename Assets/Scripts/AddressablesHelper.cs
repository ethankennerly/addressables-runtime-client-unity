using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

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
                if (key is not string s)
                {
                    continue;
                }
                var lower = s.ToLowerInvariant();
                if (!lower.StartsWith(prefix))
                {
                    continue;
                }
                set.Add(s);
            }
        }
        var list = new List<string>(set.Count);
        foreach (var s in set)
        {
            list.Add(s);
        }
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    public static async Task<GameObject> InstantiateAsync(string address, List<GameObject> spawned)
    {
        var handle = Addressables.LoadAssetAsync<GameObject>(address);
        while (!handle.IsDone)
        {
            await Task.Yield();
        }
        bool failed = !handle.IsValid() ||
            handle.Status != AsyncOperationStatus.Succeeded ||
            handle.Result == null;
        if (failed)
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
