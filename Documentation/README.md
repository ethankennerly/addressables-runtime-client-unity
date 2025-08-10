# Remote Asset Loading with Unity Addressables

## Overview
This project demonstrates a **professional, multi-repository workflow** for building and delivering Unity content using **Addressables**.  
The solution separates content creation from the runtime client, enabling:
- Faster iteration for content teams
- Smaller client builds
- On-demand asset delivery from a remote server or local editor hosting

## Architecture
This workflow uses **two repositories**:

1. **Content Pipeline Repository**  
   [https://github.com/ethankennerly/addressables-content-pipeline-unity](https://github.com/ethankennerly/addressables-content-pipeline-unity)  
   - Used by artists and content developers  
   - Builds Addressable AssetBundles and catalogs  
   - Outputs are deployed to a `ServerData` folder, ready for hosting

2. **Runtime Client Repository**  
   [https://github.com/ethankennerly/addressables-runtime-client-unity](https://github.com/ethankennerly/addressables-runtime-client-unity)  
   - Unity project for the actual application or game  
   - Loads assets from the prebuilt catalogs and bundles at runtime  
   - Supports both **local editor testing** and **on-device testing** against remote-hosted content

## How It Works
1. **Content Creation** – Artists add prefabs, textures, models, etc., to the content repo.
2. **Indexing & Build** – Addressable groups are updated and the build generates catalogs and bundles.
3. **Deployment** – Built files in `ServerData` are uploaded to a CDN, web server, or local dev host.
4. **Runtime Loading** – The client repo loads the catalog at startup and streams assets on-demand.

## Documentation
Each repository contains its own `README.md` with setup steps:
- Content pipeline: [https://github.com/ethankennerly/addressables-content-pipeline-unity](https://github.com/ethankennerly/addressables-content-pipeline-unity#readme)
- Runtime client: [https://github.com/ethankennerly/addressables-runtime-client-unity](https://github.com/ethankennerly/addressables-runtime-client-unity#readme)

You’re reading the **project-level** README, stored at:  
`addressables-runtime-client-unity/Documentation/README.md`

## Goals
- Provide a **robust, scalable, and editor-friendly** workflow for Unity Addressables
- Enable non-programmers to build and deploy content safely
- Maintain a clean separation between build-time and runtime code

## Project Link

[Project board](https://github.com/users/ethankennerly/projects/1)

## License
MIT
