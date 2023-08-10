# NeRF2Mesh-Unity-Viewer

# project in progress 

This repository contains the source code for a Unity port of the web viewer from the paper [NeRF2Mesh: Delicate textured mesh recovery from nerf via adaptive surface refinement](https://github.com/ashawkey/nerf2mesh/)[^1]

## Usage
Tested with Unity 2022.3.0f1, windows 11.

### Installation

In `Package Manager -> Add package from git URL...` paste `https://github.com/bell-one/NeRF2Mesh-Unity-Viewer.git` [as described here](https://docs.unity3d.com/Manual/upm-ui-giturl)


## Sample Scene
lego scene from nerf2mesh stage1 (https://drive.google.com/drive/folders/1tDBtwuGUCddKIi_IJRya6QW4viPp_D3m?usp=sharing)

After installation, you can use the menu `NeRF2Mesh -> Import from disk` to import downloaded or trained scenes.

## To-do

- Test with other scenes, check for specular MLP calculations


## Acknowledgements

This project heavily borrowed from (https://github.com/julienkay/MobileNeRF-Unity-Viewer)

[^1]: [TANG, Jiaxiang, et al. Delicate textured mesh recovery from nerf via adaptive surface refinement. arXiv preprint arXiv:2303.02091, 2023.](https://https://github.com/ashawkey/nerf2mesh/)
