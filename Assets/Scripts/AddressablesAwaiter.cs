using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class AddressablesAwaiter
{
    public static async Task Await(AsyncOperationHandle handle)
    {
        while (!handle.IsDone)
        {
            await Task.Yield();
        }
    }

    public static async Task Await<T>(AsyncOperationHandle<T> handle)
    {
        while (!handle.IsDone)
        {
            await Task.Yield();
        }
    }

    public static void SafeRelease(AsyncOperationHandle handle)
    {
        if (handle.IsValid())
        {
            Addressables.Release(handle);
        }
    }

    public static void SafeRelease<T>(AsyncOperationHandle<T> handle)
    {
        if (handle.IsValid())
        {
            Addressables.Release(handle);
        }
    }

    public static void SafeReleaseInstance(GameObject go)
    {
        if (go)
        {
            Addressables.ReleaseInstance(go);
        }
    }
}
