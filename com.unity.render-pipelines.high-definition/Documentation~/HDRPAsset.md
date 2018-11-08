# The High Definition Render Pipeline Asset

The High Definition Render Pipeline Asset (HDRP Asset) controls the global rendering settings of your project and creates the rendering pipeline instance. The rendering pipeline instance contains intermediate resources and the render loop implementation. For more information about the rendering pipeline instance, see documentation on[ Rendering Pipelines](http://placeholder).

## Creating a HDRP Asset

A new project using the HDRP template includes a HDRP Asset file named HDRenderPipelineAsset in the Assets Settings folder.

If you create a new project and do not use the HDRP template, follow the steps below to create, and customize, your own HDRP Asset. Creating a custom HDRP Asset will stop future HDRP package updates overwriting your render pipeline settings.

- Navigate to the folder you want to create your HDRP Asset in. This must be somewhere inside the Assets folder; you can not create a HDRP Asset in the Packages folder.
- In the menu, navigate to Assets Create Rendering and click High Definition Render Pipeline Asset to create your HDRP Asset.
- Name the HDRP Asset and press the Return key to confirm it.

Now that you have created a HDRP Asset, you must assign it it to the Scriptable Render Pipeline Settings field in your Unity Project's Graphic Settings. In the menu, navigate to Edit Project Settings Graphics and locate the Scriptable Render Pipeline Settings field at the top. To assign your HDRP Asset to this field, either drag and drop it into the field, or use the object picker (located on the right of the field) to select it from a list of all HDRP Assets in your Unity Project.

You can create multiple HDRP Assets containing different settings. You can change the HDRP Asset your render pipeline uses by either manually selecting a Pipeline Asset in the Graphics Settings window (as shown above), or by using the GraphicsSettings.renderPipelineAsset property via script.


Creating multiple HDRP Assets is useful when developing for multiple platforms. You can create a HDRP asset for each platform your Unity Project supports (for example, PC, Xbox One, PlayStation 4). In each HDRP Asset, you can change settings to suite the hardware of each platform and then assign the relevant one when building your Project for each platform.

 ![](Images\HDRPAsset1.png)


Now that you have created a HDRP Asset, you must assign it it to the __Scriptable Render Pipeline Settings__ field in your Unity Project's Graphic Settings. In the menu, navigate to **Edit &gt; Project Settings &gt; Graphics** and locate the __Scriptable Render Pipeline Settings__ field at the top. To assign your HDRP Asset to this field, either drag and drop it into the field, or use the object picker (located on the right of the field) to select it from a list of all HDRP Assets in your Unity Project.

 

You can create multiple HDRP Assets containing different settings. You can manually select a __Scriptable Render Pipeline Asset__ in the Graphics Settings window, or you can use GraphicsSettings.renderPipelineAsset](API Link) property via script. 


You must create a new asset for each platform your project will support (PC, Xbox One, Playstation 4, etc) and then assign the relevant Asset when you build your project for each platform. 


## Render Pipeline Resources

The High Definition Render Pipeline Resources Asset (HDRP Resources Asset) stores references to Shaders and Materials used by HDRP.  When you build your Unity Project, it will embed all of the resources the HDRP Resources Asset references. This is the Scriptable Render Pipeline equivalent of the Legacy Resources folder mechanism of Unity. It is useful because it allows you to set up multiple render pipelines in a Unity Project and, when you build the Project, Unity only embeds Shaders and Materials relevant for that pipeline.

Unity creates a HDRP Resources Asset when you create a HDRP Asset and references it automatically. You can create a HDRP Resource Asset manually by navigating to Assets Create Rendering and clicking High Definition Render Pipeline Resources.

## Diffusion Profile Settings

The Diffusion Profile settings Asset stores Subsurface Scattering and Transmission Control profiles for your project. Create a Diffusion Profile Settings Asset by navigating to Assets Create Rendering and clicking Diffusion Profile Settings.

## Enable Shader Variant Stripping

<What does this do?>

## Render Pipeline Settings

These settings enable or disable HDRP features in your Unity Project. Unity will not load Assets for disabled features. Disabled features cannot be enabled during run time.

Use these settings to save memory by disabling features you are not using.

| Property                                      | Function                                                     |
| --------------------------------------------- | ------------------------------------------------------------ |
| Support Shadow Mask                           | Tick this checkbox to enable support for[ Shadowmask](https://docs.unity3d.com/Manual/LightMode-Mixed-Shadowmask.html) in your project. |
| Support SSR (screen space reflection)         | Tick this checkbox to enable support for[ SSR](https://docs.unity3d.com/Manual/PostProcessing-ScreenSpaceReflection.html). |
| Support SSAO (screen space ambient occlusion) | Tick this checkbox to enable support for[ SSAO](http://placeholder). <I don't see a document for this in the MVP sheet should we |
| Support Subsurface Scattering                 | Tick this checkbox to enable Subsurface Scattering. light penetrates the surface of a translucent object |
| Increase SSS Sample Count                     | Tick this checkbox to increase SSS Sample Count  <What does it increase it from and to?> |
| Support volumetrics                           | Tick this checkbox to enable support for volumetrics <Is this just for volumetric lighting?>. |
| Increase resolution of volumetrics            | Tick this checkbox to increase the resolution of volumetrics <What does it increase it from and to?> |
| Support LightLayers                           | Tick this checkbox to enable support for LightLayers. You can assign a Layer to a Light and it will only light up Mesh Renderers with a matching rendering Layer. |
| Support Only Forward                          | Tick this checkbox to only support[ forward rendering](https://docs.unity3d.com/Manual/RenderTech-ForwardRendering.html). |
| Support Decals                                | Tick this checkbox to enable support for Decals.             |
| Support Motion Vectors                        | Tick this checkbox to enable support for Motion Vectors. <Does this allow per-object motion vector pass used for things like motion blur? Or is it something else?> |
| Support Stereo Rendering                      | Tick this box to enable[ Stereo rendering](https://docs.unity3d.com/Manual/SinglePassStereoRendering.html) for VR projects. |
| Support runtime debug display                 | Tick this checkbox to enable support for the runtime debug display. <Is this the console that you get with a development build?> |
| Support dithering cross fade                  | Tick this checkbox to enable support for dithering cross fade. <What is dithering cross fade?> |

## Cookies

Use the Cookie settings to configure the maximum resolution of cookies and texture arrays. Larger sizes use more memory, but result in higher quality images.

| Property           | Function                                                     |
| ------------------ | ------------------------------------------------------------ |
| Cookie Size        | The maximum Cookie size.                                     |
| Texture Array Size | The maximum Texture Array size                               |
| Point Cookie Size  | The maximum[ Point Cookie](https://docs.unity3d.com/Manual/Cookies.html) size. |
| Cubemap Array Size | The maximum[ Spot Cookie](https://docs.unity3d.com/Manual/Cookies.html) size. <This is from Rob's draft. I thought Cubemaps were used for point light cookies and 2D for Spot and Directional?> |

## Reflection

Use the Reflection settings to configure the resolution of your reflections and whether Unity should compress the probe caches or not.

| Property                               | Function                                                     |
| -------------------------------------- | ------------------------------------------------------------ |
| Compress Reflection Probe Cache        | Tick this checkbox to compress the Reflection Probe Cache.   |
| Reflection Cubemap Size                | The maximum resolution of the Reflection[ Cubemap](https://docs.unity3d.com/Manual/class-Cubemap.html). |
| Probe Cache Size                       | The maximum resolution of the[ Probe Cache](http://placeholder). <What does increasing this value do?> |
| Compress Planar Reflection Probe Cache | Tick this checkbox to compress the Planar Reflection Probe Cache. |
| Planar Reflection Texture Size         | The maximum resolution of the Planar Reflection texture. <What does increasing this value do?> |
| Planar Probe Cache Size                | Tick this checkbox to compress the Planar Probe Cache.       |

## Sky

These settings control skybox reflections and skybox lighting.

| Property                   | Function                              |
| -------------------------- | ------------------------------------- |
| Sky Reflection Size        | <What does increasing this value do?> |
| Sky Lighting Override Mask | <What does this LayerMask do?>        |

## Shadow Settings

These settings adjust the size of the shadow mask. Smaller values will cause Unity to discard more distant shadows, while higher values will lead to Unity displaying more shadows at longer distances from the camera. 

Higher values will use more memory.

## Shadow Atlas

| Property           | Function                                                     |
| ------------------ | ------------------------------------------------------------ |
| Atlas Width        | The Shadow Atlas width. <What happens if you increase this number?> |
| Atlas Height       | The Shadow Atlas height. <Guessing similar effect to the above> |
| 16-bit Shadow Maps | <Does this allow 16-bit Shadow Maps, or force them?>         |

### Shadow Map Budget

| Property                      | Function                                                     |
| ----------------------------- | ------------------------------------------------------------ |
| Max Point Light Shadows       | <Are these the maximum number of Lights that can cast shadows, or are they the maximum number of shadows every Light can cast?> |
| Max Spot Light Shadows        | (The maximum number of shadows cast by each Spot Light.) OR (The maximum number of Spot Lights that can cast shadows.)? |
| Max Directional Light Shadows |                                                              |

## Decals

These settings control the draw distance and resolution of decals.

| Property                       | Function                                                     |
| ------------------------------ | ------------------------------------------------------------ |
| Draw Distance                  | The maximum distance from the Camera at which Unity will draw Decals. |
| Atlas Width                    | The Decal Atlas width.                                       |
| Atlas Height                   | The Decal Atlas height.                                      |
| Enable Metal and AO properties | <What does this do? What Properties?>                        |

## Rendering Passes

These settings enable or disable the rendering passes made by the main Camera. Disabling these settings does not save on memory, but can improve performance.

You can enable or disable these settings during run time.

| Property                     | Function                                                     |
| ---------------------------- | ------------------------------------------------------------ |
| Enable Transparent Prepass   | Tick this checkbox to enable Transparent Prepass.  <What is the difference between this and the property below?> |
| Enable Transparent Postpass  | Tick this checkbox to enable Transparent Postpass.           |
| Enable Motion Vectors        | Tick this checkbox to enable Motion Vectors. <What is the difference between this and the property below? Are either the same as the one in Render Pipeline Settings at the top?> |
| Enable Object Motion Vectors | Tick this checkbox to enable Object Motion Vectors.          |
| Enable DBuffer               | Tick this checkbox to enable <Is this the Depth Buffer? If so, can you give an example of why you would have this disabled?> |
| Enable Rough Refraction      | Tick this checkbox to enable Rough Refraction. <What does this do?> |
| Enable Distortion            | Tick this checkbox to enable Distortion. <Distortion of what?> |
| Enable Postprocess           | Tick this checkbox to enable Postprocessing. <With this disabled, is all postprocessing disabled too?> |

## Rendering Settings

<What are these settings for? Are they for all Cameras?>

<Can theses be enabled/disabled at run time?>

| Property                                    | Function                                                     |
| ------------------------------------------- | ------------------------------------------------------------ |
| Enable Forward Rendering Only               | Tick this checkbox to only use forward rendering             |
| Enable Depth Prepass With Deferred Renderer | Disable Forward Rendering Only to access this property. <What does this do?> |
| Enable Async Compute                        | Tick this checkbox to enable Async Compute.                  |
| Enable Opaque Objects                       | Tick this checkbox to enable Opaque GameObjects.             |
| Enable Transparent Objects                  | Tick this checkbox to enable Transparent GameObjects.        |

## Lighting Settings

Use these settings to enable or disable Light features.

<Can theses be enabled/disabled at run time?>

| Property                      | Function                                                     |
| ----------------------------- | ------------------------------------------------------------ |
| Enable Shadow                 | Tick this checkbox to enable Shadows.                        |
| Enable Contact Shadows        | Tick this checkbox to enable Contact Shadows.                |
| Enable Shadow Masks           | Tick this checkbox to enable Shadow Masks.                   |
| Enable SSR                    | Tick this checkbox to enable Screen Space Reflections.       |
| Enable SSAO                   | Tick this checkbox to enable Screen Space Ambient Occlusion. |
| Enable Subsurface Scattering  | Tick this checkbox to enable Subsurface Scattering. With this enabled, Unity simulates how light penetrates surfaces of translucent GameObjects, scatters inside them, and exits from different locations. |
| Enable Transmission           | Tick this checkbox to enable Transmission. <What does this do?> |
| Enable Atmospheric Scattering | Tick this checkbox to enable Atmospheric Scattering.<What does this do?> |
| Enable Volumetric             | Tick this checkbox to enable Volumetric. <This say Volumetric but in the Render Pipeline Settings it says Volumetrics, should they be different?> |
| Enable LightLayers            | Tick this checkbox to enable LightLayers.                    |

## Light Loop Settings

Use these settings to enable or disable.... <What would be a good name to group these?>

<Can theses be enabled/disabled at run time?>

| Property                         | Function                                                     |
| -------------------------------- | ------------------------------------------------------------ |
| Enable FPTL For Forward Opaque   | Tick this checkbox to enable FPTL For Forward Opaque. <What is this?> |
| Enable Big Tile Prepass          | Tick this checkbox to enable Big Tile Prepass. <What is this?> |
| Enable Compute Light Evaluation  | Tick this checkbox to enable Compute Light Evaluation.<What is this?> |
| Enable Compute Light Variants    | Enable Compute Light Evaluation to access this property. Tick this checkbox to enable Compute Light Variants. <What is this?> |
| Enable Compute Material Variants | Enable Compute Light Evaluation to access this property. Tick this checkbox to enable Compute Material Variants. <What is this?> |