# NeRF2Mesh-Unity-Viewer

This repository contains the source code for a Unity port of the web viewer from the paper [NeRF2Mesh: Delicate textured mesh recovery from nerf via adaptive surface refinement](https://github.com/ashawkey/nerf2mesh/)[^1]

## Usage
Tested with Unity 2022.3.0f1, windows 11.

### Installation

Go to the [releases section](https://https://github.com/bell-one/NeRF2Mesh-Unity-Viewer/releases/latest), download the Unity Package, and import it into any Unity project. This is a 'Hybrid Package' that will install into your project as a local package.

##### Alternatives

<details>
  <summary> UPM Package via Git URL </summary>
  
  In `Package Manager -> Add package from git URL...` paste `https://github.com/bell-one/NeRF2Mesh-Unity-Viewer.git` [as described here](https://docs.unity3d.com/Manual/upm-ui-giturl)
</details>


## Sample Scene
lego scene from nerf2mesh stage1 (https://drive.google.com/drive/folders/1tDBtwuGUCddKIi_IJRya6QW4viPp_D3m?usp=sharing)

After installation, you can use the menu `NeRF2Mesh -> Import from disk` to import downloaded or trained scenes.

## To-do

- Textures are sometimes not automatically loaded into materials
- Test with other scenes


## Acknowledgements

This project heavily borrowed from (https://github.com/julienkay/MobileNeRF-Unity-Viewer)

[^1]: [TANG, Jiaxiang, et al. Delicate textured mesh recovery from nerf via adaptive surface refinement. arXiv preprint arXiv:2303.02091, 2023.](https://https://github.com/ashawkey/nerf2mesh/)
