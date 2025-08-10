using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;
using UnityEngine.Networking;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.ResourceManagement.ResourceLocations;

[Serializable] class PackDto { public string id; public string title; public string releaseUtc; public string catalogFile; }
[Serializable] class ManifestDto { public int version = 1; public PackDto[] packs; }

public sealed class ContentRuntimeLoader : MonoBehaviour
{
    [Header("Content entrypoint")]
    [Tooltip("Production mobile: HTTPS base that hosts packs.json and catalogs.\nEditor: you may set a local folder path for fast iteration.")]
    public string BaseUrlOrPath = "";

    [Header("Demo behavior")]
    public bool InstantiateFirstFurniture = true;

    // Tunables for mobile
    private const int TIMEOUT_SEC = 10;
    private const int MAX_RETRIES = 2;
    private static readonly WaitForSeconds BACKOFF = new WaitForSeconds(0.6f);

    private const string SLOT_PREFIX = "slot_";

    private readonly List<IResourceLocator> _locators = new();
    private readonly List<GameObject> _spawned = new();

    private void Start() => StartCoroutine(Bootstrap());

    private IEnumerator Bootstrap()
    {
        if (string.IsNullOrEmpty(BaseUrlOrPath))
        {
            Debug.LogError("[Runtime] BaseUrlOrPath is empty.");
            yield break;
        }

        // Production: initialize Addressables first on device builds
#if !UNITY_EDITOR
        {
            var init = Addressables.InitializeAsync();
            yield return init;
            if (!init.IsValid() || init.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("[Runtime] Addressables.InitializeAsync failed.");
                yield break;
            }
        }
#else
        // Editor: we’ll allow implicit init via first catalog load, but it’s fine either way
#endif

        // 1) Acquire packs.json (HTTPS on device; file path allowed in Editor)
        string packsJson = null;
#if UNITY_EDITOR
        if (!IsHttp(BaseUrlOrPath))
        {
            var path = Path.Combine(BaseUrlOrPath, "packs.json");
            if (!File.Exists(path)) { Debug.LogError("[Runtime] packs.json not found at " + path); yield break; }
            try { packsJson = File.ReadAllText(path); } catch (Exception e) { Debug.LogError("[Runtime] packs.json read failed: " + e.Message); yield break; }
        }
        else
#endif
        {
            // Device/web builds must use HTTPS in production
            if (!IsHttps(BaseUrlOrPath)) { Debug.LogError("[Runtime] Mobile requires HTTPS. Update BaseUrlOrPath."); yield break; }
            var url = Join(BaseUrlOrPath, "packs.json");
            yield return StartCoroutine(HttpGetText(url, t => packsJson = t));
            if (packsJson == null) { Debug.LogError("[Runtime] Could not fetch packs.json."); yield break; }
        }

        var manifest = JsonUtility.FromJson<ManifestDto>(packsJson);
        if (manifest?.packs == null || manifest.packs.Length == 0) { Debug.LogError("[Runtime] packs.json contained no packs."); yield break; }

        // 2) Select released packs
        var nowUtc = DateTime.UtcNow;
        var eligible = manifest.packs.Where(p => TryParseUtc(p.releaseUtc, out var when) && when <= nowUtc).ToList();
        if (eligible.Count == 0) { Debug.LogWarning("[Runtime] No released packs yet."); yield break; }

        // 3) Load binary catalogs (auto-heal .json → .bin)
        foreach (var pack in eligible)
        {
            string catFile = pack.catalogFile;
            if (catFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                catFile = Path.ChangeExtension(catFile, ".bin");

#if UNITY_EDITOR
            if (!IsHttp(BaseUrlOrPath))
            {
                var filePath = Path.Combine(BaseUrlOrPath, catFile);
                if (!File.Exists(filePath)) { Debug.LogError("[Runtime] Catalog missing: " + filePath); yield break; }
                var catOpFile = Addressables.LoadContentCatalogAsync(filePath);
                yield return catOpFile;
                if (catOpFile.Status != AsyncOperationStatus.Succeeded || catOpFile.Result == null) { Debug.LogError("[Runtime] Catalog load failed: " + filePath); yield break; }
                _locators.Add(catOpFile.Result);
                continue;
            }
#endif
            var url = Join(BaseUrlOrPath, catFile);
            var catOp = Addressables.LoadContentCatalogAsync(url);
            yield return catOp;
            if (catOp.Status != AsyncOperationStatus.Succeeded || catOp.Result == null) { Debug.LogError("[Runtime] Catalog load failed: " + url); yield break; }
            _locators.Add(catOp.Result);
        }

        // 4) Minimal demo: spawn first room + first furniture
        var rooms = GetAddressesByPrefix("rooms/");
        var furns = GetAddressesByPrefix("furniture/");
        if (rooms.Count == 0) { Debug.LogWarning("[Runtime] No rooms/ addresses found."); yield break; }

        yield return InstantiateRoomWithSlots(rooms[0]);

        Debug.Log($"[Runtime] Ready. catalogs={_locators.Count} rooms={rooms.Count} furniture={furns.Count}");
    }

    private IEnumerator InstantiateRoomWithSlots(string roomAddress)
    {
        // Load and spawn the room
        var roomHandle = Addressables.LoadAssetAsync<GameObject>(roomAddress);
        yield return roomHandle;
        if (!roomHandle.IsValid() || roomHandle.Status != AsyncOperationStatus.Succeeded || roomHandle.Result == null)
        {
            Debug.LogWarning("[Runtime] Load failed: " + roomAddress);
            yield break;
        }

        var roomGO = Instantiate(roomHandle.Result, Vector3.zero, Quaternion.identity);
        _spawned.Add(roomGO);
        Addressables.Release(roomHandle);

        // Fill slots with furniture based on category labels
        yield return SpawnFurnitureForSlots(roomGO);
    }

    private IEnumerator SpawnFurnitureForSlots(GameObject room)
    {
        if (!room) yield break;

        var transforms = room.GetComponentsInChildren<Transform>(true);
        int slots = 0, spawned = 0;

        foreach (var t in transforms)
        {
            if (!t || string.IsNullOrEmpty(t.name)) continue;
            if (!t.name.StartsWith(SLOT_PREFIX, StringComparison.OrdinalIgnoreCase)) continue;

            if (TryGetSlotCategory(t.name, out var category))
            {
                slots++;
                yield return SpawnOneForSlot(t, category, () => spawned++);
            }
            else
            {
                Debug.LogWarning($"[Runtime] Slot name malformed (expected 'slot_<category>[_anything]'): {t.name}");
            }
        }

        Debug.Log($"[Runtime] Slot fill complete. slots={slots} spawned={spawned}");
    }

    private IEnumerator SpawnOneForSlot(Transform slot, string category, System.Action onSpawned = null)
    {
        string label = $"furniture:{category}";

        // Query locations by label (cheap) and choose one
        var locHandle = Addressables.LoadResourceLocationsAsync(label, typeof(GameObject));
        yield return locHandle;
        if (locHandle.Status != AsyncOperationStatus.Succeeded || locHandle.Result == null || locHandle.Result.Count == 0)
        {
            Debug.LogWarning($"[Runtime] No furniture found for label '{label}'. Slot: {slot.name}");
            Addressables.Release(locHandle);
            yield break;
        }

        var locations = locHandle.Result;
        var chosen = locations[UnityEngine.Random.Range(0, locations.Count)];
        Debug.Log($"[Runtime] Spawning furniture for '{label}' → primary='{chosen.PrimaryKey}', id='{chosen.InternalId}' at slot '{slot.name}'");

        // Instantiate via Addressables to keep dependency chain intact
        var instHandle = Addressables.InstantiateAsync(chosen, slot, false);
        yield return instHandle;

        Addressables.Release(locHandle);

        if (instHandle.Status == AsyncOperationStatus.Succeeded && instHandle.Result != null)
        {
            var go = instHandle.Result;
            // Ensure local transform is clean under the slot
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;
            _spawned.Add(go);
            onSpawned?.Invoke();

            // Quick mesh audit for diagnostics
            int missing = 0;
            var mfs = go.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in mfs) if (mf && mf.sharedMesh == null) missing++;
            if (missing > 0)
                Debug.LogWarning($"[Runtime] Spawned '{label}' with {missing} MeshFilter(s) missing meshes. primary='{chosen.PrimaryKey}' id='{chosen.InternalId}'");
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

    private IEnumerator InstantiateFurnitureAt(string address, Vector3 position)
    {
        var handle = Addressables.LoadAssetAsync<GameObject>(address);
        yield return handle;
        if (!handle.IsValid() || handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            Debug.LogWarning("[Runtime] Furniture load failed: " + address);
            yield break;
        }

        var go = Instantiate(handle.Result, position, Quaternion.identity);
        _spawned.Add(go);
        Addressables.Release(handle);
    }

    // -------- HTTP (HTTPS required for device) --------
    private IEnumerator HttpGetText(string url, Action<string> onOk)
    {
        for (int attempt = 0; attempt <= MAX_RETRIES; attempt++)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = TIMEOUT_SEC;
                var op = req.SendWebRequest();
                yield return op;

                if (req.result == UnityWebRequest.Result.Success)
                {
                    onOk?.Invoke(req.downloadHandler.text);
                    yield break;
                }
            }
            if (attempt < MAX_RETRIES) yield return BACKOFF;
        }
        onOk?.Invoke(null);
    }

    // -------- Address helpers --------
    private List<string> GetAddressesByPrefix(string prefix)
    {
        prefix = prefix.ToLowerInvariant();
        var set = new HashSet<string>();
        foreach (var loc in Addressables.ResourceLocators)
            foreach (var key in loc.Keys)
                if (key is string s && s.ToLowerInvariant().StartsWith(prefix))
                    set.Add(s);
        return set.OrderBy(s => s).ToList();
    }

    private IEnumerator InstantiateAddress(string address, Vector3? position = null)
    {
        var h = Addressables.LoadAssetAsync<GameObject>(address);
        yield return h;
        if (!h.IsValid() || h.Status != AsyncOperationStatus.Succeeded || h.Result == null)
        { Debug.LogWarning("[Runtime] Load failed: " + address); yield break; }

        var go = Instantiate(h.Result, position ?? Vector3.zero, Quaternion.identity);
        _spawned.Add(go);
        Addressables.Release(h);
    }

    // -------- utils --------
    private static bool IsHttp(string s)  => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    private static bool IsHttps(string s) => s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    private static string Join(string basePath, string file) => IsHttp(basePath) ? (basePath.EndsWith("/") ? basePath + file : basePath + "/" + file) : Path.Combine(basePath, file);
    private static bool TryParseUtc(string s, out DateTime utc)
    {
        if (DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt))
        { utc = dt.ToUniversalTime(); return true; }
        utc = default; return false;
    }

    private void OnDestroy()
    {
        foreach (var go in _spawned)
        {
            if (!go) continue;
            try { Addressables.ReleaseInstance(go); } catch { }
            if (go) Destroy(go);
        }
        _spawned.Clear();

        foreach (var loc in _locators)
        {
            if (loc != null)
            {
                try { Addressables.RemoveResourceLocator(loc); } catch { }
            }
        }
        _locators.Clear();
    }
}