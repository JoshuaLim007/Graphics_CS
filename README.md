

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
- SSGI
- Thin G-Buffer
- Cache model-matrices on static objects
- TAA (WIP)
- Procedural Physical based Sky 

## Todo
### High priority
- Move over to glMultiDrawElementsIndirect
- SSR
- IBL (image based lighting)

### Medium priority
- Screen space subsurface scattering
- Order independent transparents
- Volumetric lighting
- Physical based procedural sky
- Volumetric Clouds
- Water/Ocean rendering

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

![enter image description here](https://i.imgur.com/XgwgH5L.png)

![enter image description here](https://i.imgur.com/DnbvUlu.png)

![enter image description here](https://i.imgur.com/s3aBQ4X.png)

![enter image description here](https://i.imgur.com/UENQT54.png)
