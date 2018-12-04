# HD Shadow Settings

The HD Shadow Settings Volume component override control the maximum distance at which HDRP renders shadow cascades and shadows from [punctual lights](Glossary.html#PunctualLights). It uses cascade splits to control the quality of shadows cast by Directional Lights over distance from the Camera.

This HD Shadow Settings override comes as default when you create a __Scene Settings__ GameObject (Menu: __GameObject > Rendering > Scene Settings__).

![](Images/SceneSettingsHDShadowSettings1.png)


| Property          | Description                                                  |
| :---------------- | :----------------------------------------------------------- |
| __Max Distance__  | The maximum distance at which the HDRP renders shadow. HDRP uses this for punctual Lights and as the last boundary for the final cascade. |
| __Cascade Count__ | The number of cascades for Direction Lights that can cast shadows. Cascades work as a shadow LOD, shadows at distances |
| __Split 1__       | The limit between the first and second cascade split (expressed as a percentage of Max Distance) |
| __Solit 2__       | The limit between the second and third cascade split (expressed as a percentage of Max Distance) |
| __Split 3__       | The limit between the third and final split (expressed as a percentage of Max Distance) |
