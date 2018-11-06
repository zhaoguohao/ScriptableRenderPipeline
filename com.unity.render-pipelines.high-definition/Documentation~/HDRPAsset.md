# The High Definition Render Pipeline Asset

The High Definition Render Pipeline Asset (HDRP Asset) controls the global rendering settings of your project and creates the rendering pipeline instance. The rendering pipeline instance contains intermediate resources and the render loop implementation. For more information about the rendering pipeline instance, see documentation on [Rendering Pipelines](http://placeholder).

 

# Creating a HDRP Asset

A new project using the HDRP template includes a HDRP Asset file named **HDRenderPipelineAsset** in the **Assets &gt; Settings** folder. 

If you create a new project and do not use the HDRP template, follow the steps below to create, and customize, your own HDRP Asset. Creating a custom HDRP Asset will stop future HDRP package updates overwriting your render pipeline settings. 

 

- Navigate to the folder you want to create your HDRP Asset in. This must be somewhere inside the __Assets__ folder; you can not create a HDRP Asset in the __Packages__ folder.
- In the menu, navigate to __Assets &gt; Create &gt; Rendering__ and click __High Definition Render Pipeline Asset__ to create your HDRP Asset.
- Name the HDRP Asset and press the Return key to confirm it.



 ![](Images\HDRPAsset1.png)

 

Now that you have created a HDRP Asset, you must assign it it to the __Scriptable Render Pipeline Settings__ field in your Unity Project's Graphic Settings. In the menu, navigate to **Edit &gt; Project Settings &gt; Graphics** and locate the __Scriptable Render Pipeline Settings__ field at the top. To assign your HDRP Asset to this field, either drag and drop it into the field, or use the object picker (located on the right of the field) to select it from a list of all HDRP Assets in your Unity Project.

 

You can create multiple HDRP Assets containing different settings. Assets can be set by either manually selecting a Pipeline Asset in the Graphics Settings window, or by using the [GraphicsSettings.renderPipelineAsset](API Link) property via script. 

 

You must create a new asset for each platform your project will support (PC, Xbox One, Playstation 4, etc) and then assign the relevant Asset when you build your project for each platform. 

![img](https://lh3.googleusercontent.com/AaJXJerlJyziXT7UYqeeHltn40fmpND0ZjsU4uxv9FvevFb4V2hwBJ2yRksOKVB7x9tGFCLiDGc-xXgVGoTws_rUHkG3CY6vIyMd5qF_5oRENJ3vwbdTqTlNm0cc-wVbCZiiLwVd)

## Render Pipeline Resources

The HD Render Pipeline Resources Asset stores references to shaders and materials used by HDRP. When a player is build, it will embed all this referenced resources. This is in replacement of the Resources folder mechanism of Unity as multiple render pipeline could be setup in a project and the player must not embed all these resources. 

 

There is always a rende pipeline resource available. The HDRP Asset will reference it automatically on creation. It is possible but not recommended to create a HDRP resource asset with the same command than for HDRP Asset. This asset shouldn’t be modify unless a shader need to be replace by a custom version.

## Diffusion Profile Settings

The Diffusion Profile settings Asset stores Subsurface Scattering and Transmission Control profiles for your project. For further information about the Diffusion Profile Settings asset, refer to documentation on [Subsurface Scattering](http://placeholder).

## Render Pipeline Settings

These settings enable or disable HDRP features in your project. Assets for disabled features will not be loaded in your project and disabled features cannot be enabled during run time. 

 

Use these settings to save memory by disabling features you are not using. 

 

| Property                                      | Function                                                     |
| --------------------------------------------- | ------------------------------------------------------------ |
| Support Shadow Mask                           | Tick this checkbox to enable [Shadowmask](https://docs.unity3d.com/Manual/LightMode-Mixed-Shadowmask.html) in your project. |
| Support SSR (Screen space reflection)         | Tick this checkbox to enable [SSR](https://docs.unity3d.com/Manual/PostProcessing-ScreenSpaceReflection.html). |
| Support SSAO (Screen space ambient occlusion) | Tick this checkbox to enable [SSAO](http://placeholder).     |
| Support Decal Buffer                          | Tick this checkbox to enable Decal Buffer.                   |
| Support Multi Sampling Anti-Aliasing (MSAA)   | Tick this checkbox to enable MSAA.                           |
| MSAA Sample Count                             | Specifies the MSAA sample count. Options are MSAA x2 MSAA x4 and MSAA x8. |
| Support Subsurface Scattering                 | Tick this checkbox to enable Subsurface Scattering.          |
| Support Forward Only                          | Tick this checkbox to only support [forward rendering](https://docs.unity3d.com/Manual/RenderTech-ForwardRendering.html). |
| Support Motion Vectors                        | Tick this checkbox to enable motion vectors.                 |
| Support Stereo Rendering                      | Tick this box to enable [stereo rendering](https://docs.unity3d.com/Manual/SinglePassStereoRendering.html) for VR projects. |
| Enable Ultra Quality SSS                      | Tick this box to enable ultra quality Subsurface Scattering. |

 

## Cookies

Use the Cookie settings to configure the maximum resolution of cookies and texture arrays. Larger sizes use more memory, but result in higher quality images. 

 

| Property           | Function                                                     |
| ------------------ | ------------------------------------------------------------ |
| Cookie Size        | The Maximum Cookie size.                                     |
| Texture Array Size | The maximum Texture Array size                               |
| Point Cookie Size  | The maximum [Point Cookie](https://docs.unity3d.com/Manual/Cookies.html) size |
| Cubemap Array Size | The maximum [Spot Cookie](https://docs.unity3d.com/Manual/Cookies.html) size |

 

## Reflection

Reflection settings <do stuff>

 

| Property                               | Function                                                     |
| -------------------------------------- | ------------------------------------------------------------ |
| Compress Reflection Probe Cache        | Tick this checkbox to compress the Reflection Probe Cache. |
| Reflection Cubemap Size                | The maximum resolution of the Reflection [Cubemap](https://docs.unity3d.com/Manual/class-Cubemap.html) |
| Probe Cache Size                       | The maximum resolution of the [Probe Cache](http://placeholder) |
| Compress Planar Reflection Probe Cache | Tick this checkbox to compress the Planar Reflection Probe Cache. |
| Planar Reflection Texture Size         | The maximum resolution of the Planar Reflection texture.     |
| Planar Probe Cache Size                | Tick this checkbox to compress the Planar Probe Cache. |
| Max Planar Probe Per Frame             | The amount of Planar Probes per frame.                       |

 

## Sky

These settings control skybox reflections and skybox lighting. 

 

| Property                   | Function |
| -------------------------- | -------- |
| Sky Reflection Size        |          |
| Sky Lighting Override Mask |          |

 

## Shadow Atlas Settings

These settings adjust the size of the shadow mask. Smaller values will cause more distant shadows to be discarded, while higher values will lead to more shadows being displayed at longer distances from the camera. 

 

Higher values will use more memory.

 

| Property     | Function                |
| ------------ | ----------------------- |
| Atlas Width  | The Shadow Atlas width  |
| Atlas Height | The Shadow Atlas height |

## Decals

 

| Property      | Function |
| ------------- | -------- |
| Draw Distance |          |
| Atlas Size    |          |

 

## Rendering Passes

![img](https://lh3.googleusercontent.com/FceW7tpqECr28mZXqGyr1P_OrHKaktGJMA2FtHsQQG1aQgx_-RradTAN8xzlpZ4eC-Ia3JPoWydkmMDGvVnfN4L0k4SrFZKJRu1p4TXsT93TeoQb0lRx7x6CWI6k6xGG6OMsQRbQ)

 

These settings enable or disable the rendering passes made my the main camera. Disabling these settings does not save on memory, but can improve performance. 

 

These settings can be enabled or disabled during run time.

 

| Property                      | Function |
| ----------------------------- | -------- |
| Enable Transparent Prepass    |          |
| Enable Transparent Postpass   |          |
| Enable Motion Vectors         |          |
| Enable Object Motion Vectors  |          |
| Enable DBuffer                |          |
| Enable Atmospheric Scattering |          |
| Enable Rough Refraction       |          |
| Enable Distortion             |          |
| Enable Postprocess            |          |

 

## Rendering Settings

![img](https://lh4.googleusercontent.com/o4UxGd5zkgXty8ugYvw1pfHmORnTm_MddUvXOVlGeFlQhHs4O9KVrLf5z9dGCtXcRhJLRvcVlSbPjAvPmihTrjxk9mNpbjeIbOi5QGRblIHNno3_ZD-dtL0BhFY_C_e1nlFAnK5m)

 

| Property                                    | Function |
| ------------------------------------------- | -------- |
| Enable Forward Rendering Only               |          |
| Enable Depth Prepass With Deferred Renderer |          |
| Enable Async Compute                        |          |
| Enable Opaque Objects                       |          |
| Enable Transparent Objects                  |          |
| Enable MSAA                                 |          |

 

## Lighting Settings

![img](https://lh6.googleusercontent.com/WJGKdjjD2SzNWVGc4Qv-diVRIJbksWUC9bFAegEtz8BV8O63S31zby0YoEvuGt050BVOzmBhQrFtoqJpGDLn9qzoa0G6LaAy5PrNJsqqjrJkzbBdRi0SCTZFCPwVkIi6kE2uT7Tn)

 

| Property                     | Function                                                    |
| ---------------------------- | ----------------------------------------------------------- |
| Enable SSR                   | Tick this checkbox to enable Screen Space Reflections       |
| Enable SSAO                  | Tick this checkbox to enable Screen Space Ambient Occlusion |
| Enable Subsurface Scattering | Tick this checkbox to enable Subsurface Scattering          |
| Enable Transmission          | Tick this checkbox to enable Transmission                   |
| Enable Shadow                | Tick this checkbox to enable Shadows                        |
| Enable Contact Shadows       | Tick this checkbox to enable Contact Shadows                |
| Enable Shadow Masks          | Tick this checkbox to enable Shadow Masks                   |

 

## Light Loop Settings

![img](https://lh3.googleusercontent.com/JJC6DDHwT0HTg7AfiDkeQHsvf1X4F-qRQaoBM1D8bq12GpmlE1B90dnPxy_PtYqGHVHq0yjE8FfzZIkC12DpjDP-notLfsFb3ZRRY4zkHvQXG2NWx9aiZpJMXg-f-w0KV4Mn5oQe)

 

| Property                         | Function |
| -------------------------------- | -------- |
| Enable FPTL For Forward Opaque   |          |
| Enable Big Tile Prepass          |          |
| Enable Compute Light Evaluation  |          |
| Enable Compute Light Variants    |          |
| Enable Compute Material Variants |          |

 