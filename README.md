# addressables-runtime-client-unity

Loads and instantiates content built with **Unity Addressables** from a remote or local location.  
This is the runtime side of a two‑repo architecture. For project purpose and collaboration, see the [Project README](./Documentation/README.md).

## Purpose
This repository contains a Unity project configured to consume Addressables catalogs and bundles produced by the content pipeline repo. It supports both local file‑based testing and remote hosting.

## Requirements
- Unity 2022.3 LTS or newer  
- Addressables package (installed via Package Manager)

## Usage

1. **Open in Unity**  
   Open this repository in the Unity Editor.

2. **Set Profile for Content Location**  
   - Go to **Window → Asset Management → Addressables → Profiles**.  
   - Select or create a profile where **Remote Load Path** points to your local file path or remote URL (matching the content pipeline output).

3. **Play Mode Test**  
   - Enter Play Mode to load catalogs and instantiate content dynamically.  
   - Ensure your hosting or file path is accessible.

4. **Build Player**  
   - Build your Unity Player as normal.  
   - Make sure the hosting/CDN contains the bundles and catalog from the content pipeline repo.

## Related Repository
Content pipeline that builds the bundles and catalog:  
[https://github.com/ethankennerly/addressables-content-pipeline-unity](https://github.com/ethankennerly/addressables-content-pipeline-unity)

## Project Docs
Project overview and collaboration docs live here:  
[https://github.com/ethankennerly/addressables-runtime-client-unity/tree/main/Documentation](https://github.com/ethankennerly/addressables-runtime-client-unity/tree/main/Documentation)

## License
MIT
