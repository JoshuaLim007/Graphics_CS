
# C# OpenGL Graphics Engine

Made with c# and openTK c# library
Unity game engine esque mono-behavior system for custom behaviors and scripts.

## Features
 - Frustum culling
 - SSAO
 - Bloom
 - Motion vectors
 - Per object motion blur
 - Directional shadows
	 - PCF
	 - Single directional light
 - Point light shadows
	 - PCF
	 - Up to 8
 - Directional lights
 - Point lights
	 - Up to 64
 - Forward rendering 
 - Optimized opengl state changes
	 - Major state change is cached
	 - program states are cached
	 - uniform values are cached
	 - Never need to state change unnecessarily
 - PBR materials
	 - Lambertian diffuse
	 - Cook-Torrence specular
- ACES tonemapping
- Skybox Cubemap
	
## Todo
### High priority
- Move over to glMultiDrawElementsIndirect
- Cache model-matrices on static objects
- TAA
- SSR
- IBL (image based lighting)

### Medium priority

- Screen space subsurface scattering
- Order independent transparents
- Volumetric lighting
- Physical based procedural sky
- Volumetric Clouds
- Water/Ocean rendering
- SSGI
- Thin G-Buffer

### Low priority
- Software Raytracing (BVH)
	- Reflections
	- Shadows
	- Global illumination
	
### Considering
- CSM (cascaded shadow maps)
- Animation (skinned renderer)
- Occlusion Culling (Hi-Z)
- Hair rendering
- Deferred rendering
- Tile based forward rendering
- Bindless textures
	- or use 2d texture arrays

## Screenshots
![sc 1](https://i.imgur.com/AKSYnbz.png)
![sc 2](https://i.imgur.com/ExQGxRk.png)
