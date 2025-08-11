using System.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// Loads content packs and instantiates demo content at runtime.
/// </summary>
public sealed class ContentRuntimeLoader : MonoBehaviour
{
    [Header("Content entrypoint")]
    [Tooltip("Base URL or local path for packs.json and catalogs.")]
    [SerializeField] private string baseUrlOrPath = "";

    private readonly List<UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator> locators = new();
    private readonly List<GameObject> spawned = new();

    private async void Start()
    {
        try
        {
            await BootstrapAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{nameof(ContentRuntimeLoader)}] Bootstrap failed: {ex}");
        }
    }

    private async Task BootstrapAsync()
    {
        if (string.IsNullOrWhiteSpace(baseUrlOrPath))
        {
            throw new InvalidOperationException("BaseUrlOrPath is empty.");
        }

        // Accept relative paths and resolve to absolute
        string resolvedBasePath = baseUrlOrPath;
        if (!IsHttp(baseUrlOrPath) && !Path.IsPathRooted(baseUrlOrPath))
        {
            // Application.dataPath points to <project>/Assets, so go up one to project root
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            resolvedBasePath = Path.GetFullPath(Path.Combine(projectRoot, baseUrlOrPath));
        }

        // 1. Load manifest
        var manifest = await ManifestLoader.LoadManifestJsonAsync(resolvedBasePath);
        var manifestDto = JsonUtility.FromJson<ManifestDto>(manifest);
        if (manifestDto?.packs == null || manifestDto.packs.Length == 0)
        {
            throw new InvalidOperationException("No packs found in manifest.");
        }

        // 2. Select eligible packs (no LINQ, no alloc)
        var eligiblePacks = new List<PackDto>(manifestDto.packs != null ? manifestDto.packs.Length : 0);
        ContentRuntimeLoader.GetEligiblePacks(manifestDto, eligiblePacks);

        // 3. Load catalogs
        foreach (var pack in eligiblePacks)
        {
            var locator = await CatalogLoader.LoadCatalogAsync(resolvedBasePath, pack.catalogFile);
            locators.Add(locator);
        }

        // 4. Demo: instantiate content
        await InstantiateDemoContentAsync();
    }
    // Accepts http(s) and file paths
    private static bool IsHttp(string s)
    {
        return s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    // Allocation-free eligible pack filter
    private static void GetEligiblePacks(ManifestDto manifest, List<PackDto> result)
    {
        result.Clear();
        var now = DateTime.UtcNow;
        var packs = manifest.packs;
        if (packs == null)
        {
            return;
        }
        foreach (var pack in packs)
        {
            DateTime release;
            if (DateTime.TryParse(pack.releaseUtc, out release) && release <= now)
            {
                result.Add(pack);
            }
        }
    }

    private async Task InstantiateDemoContentAsync()
    {
        var rooms = AddressablesHelper.GetAddressesByPrefix("rooms/");
        if (rooms.Count == 0)
        {
            Debug.LogWarning("No rooms found.");
            return;
        }

        // Instantiate the first room
        var roomHandle = Addressables.LoadAssetAsync<GameObject>(rooms[0]);
        await AwaitHandle(roomHandle);
        if (!roomHandle.IsValid() || roomHandle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded || roomHandle.Result == null)
        {
            Debug.LogWarning($"[Runtime] Load failed: {rooms[0]}");
            return;
        }

        var roomGO = GameObject.Instantiate(roomHandle.Result, Vector3.zero, Quaternion.identity);
        spawned.Add(roomGO);
        Addressables.Release(roomHandle);

        // Fill slots with furniture based on category labels
        await SpawnFurnitureForSlotsAsync(roomGO);
    }

    private async Task SpawnFurnitureForSlotsAsync(GameObject room)
    {
        if (!room)
        {
            return;
        }

        var transforms = room.GetComponentsInChildren<Transform>(true);
        int slots = 0, spawnedCount = 0;

        foreach (var t in transforms)
        {
            if (!t || string.IsNullOrEmpty(t.name))
            {
                continue;
            }
            if (!t.name.StartsWith("slot_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryGetSlotCategory(t.name, out var category))
            {
                slots++;
                await SpawnOneForSlotAsync(t, category, () => spawnedCount++);
            }
            else
            {
                Debug.LogWarning($"[Runtime] Slot name malformed (expected 'slot_<category>[_anything]'): {t.name}");
            }
        }

        Debug.Log($"[Runtime] Slot fill complete. slots={slots} spawned={spawnedCount}");
    }

    private async Task SpawnOneForSlotAsync(Transform slot, string category, Action onSpawned = null)
    {
        string label = $"furniture:{category}";

        // Query locations by label (cheap) and choose one
        var locHandle = Addressables.LoadResourceLocationsAsync(label, typeof(GameObject));
        await AwaitHandle(locHandle);
        if (locHandle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded || locHandle.Result == null || locHandle.Result.Count == 0)
        {
            Debug.LogWarning($"[Runtime] No furniture found for label '{label}'. Slot: {slot.name}");
            Addressables.Release(locHandle);
            return;
        }

        var locations = locHandle.Result;
        var chosen = locations[UnityEngine.Random.Range(0, locations.Count)];
        Debug.Log($"[Runtime] Spawning furniture for '{label}' → primary='{chosen.PrimaryKey}', id='{chosen.InternalId}' at slot '{slot.name}'");

        // Instantiate via Addressables to keep dependency chain intact
        var instHandle = Addressables.InstantiateAsync(chosen, slot, false);
        await AwaitHandle(instHandle);

        Addressables.Release(locHandle);

        if (instHandle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded && instHandle.Result != null)
        {
            var go = instHandle.Result;
            // Ensure local transform is clean under the slot
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            spawned.Add(go);
            onSpawned?.Invoke();

            // Quick mesh audit for diagnostics
            int missing = 0;
            var mfs = go.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in mfs)
            {
                if (mf && mf.sharedMesh == null)
                {
                    missing++;
                }
            }
            if (missing > 0)
            {
                Debug.LogWarning($"[Runtime] Spawned '{label}' with {missing} MeshFilter(s) missing meshes. primary='{chosen.PrimaryKey}' id='{chosen.InternalId}'");
            }
        }
        else
        {
            Debug.LogWarning($"[Runtime] Addressables.InstantiateAsync failed for '{label}' at {chosen.PrimaryKey}");
        }
        // Note: We keep instHandle implicitly via the instance; releasing happens in OnDestroy via ReleaseInstance.
    }

    private static bool TryGetSlotCategory(string slotName, out string category)
    {
        // slot_chair_01  -> category = chair
        // slot_table     -> category = table
        category = null;
        var parts = slotName.Split('_');
        if (parts.Length >= 2 && parts[0].Equals("slot", StringComparison.OrdinalIgnoreCase))
        {
            category = parts[1].Trim();
            return !string.IsNullOrEmpty(category);
        }
        return false;
    }

    private static async Task AwaitHandle(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle handle)
    {
        while (!handle.IsDone)
        {
            await Task.Yield();
        }
    }

    private void OnDestroy()
    {
        foreach (var go in spawned)
        {
            if (go)
            {
                Addressables.ReleaseInstance(go);
            }
        }
        spawned.Clear();

        foreach (var locator in locators)
        {
            if (locator != null)
            {
                Addressables.RemoveResourceLocator(locator);
            }
        }
        locators.Clear();
    }
}