using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public class DebugLightVolumes
    {
        // Material used to blit the output texture into the camera render target
        Material m_Blit;
        // Material used to render the light volumes
        Material m_DebugLightVolumeMaterial;
        // Material to resolve the light volume textures
        ComputeShader m_DebugLightVolumeCompute;
        int m_DebugLightVolumeGradientKernel;
        int m_DebugLightVolumeColorsKernel;

        // Texture used to display the gradient
        Texture2D m_ColorGradientTexture = null;

        // Shader property ids
        public static readonly int _ColorShaderID = Shader.PropertyToID("_Color");
        public static readonly int _OffsetShaderID = Shader.PropertyToID("_Offset");
        public static readonly int _RangeShaderID = Shader.PropertyToID("_Range");
        public static readonly int _DebugLightCountBufferShaderID = Shader.PropertyToID("_DebugLightCountBuffer");
        public static readonly int _DebugColorAccumulationBufferShaderID = Shader.PropertyToID("_DebugColorAccumulationBuffer");
        public static readonly int _DebugLightVolumesTextureShaderID = Shader.PropertyToID("_DebugLightVolumesTexture");
        public static readonly int _ColorGradientTextureShaderID = Shader.PropertyToID("_ColorGradientTexture");
        public static readonly int _MaxDebugLightCountShaderID = Shader.PropertyToID("_MaxDebugLightCount");

        // Render target array for the prepass
        RenderTargetIdentifier[] m_RTIDs = new RenderTargetIdentifier[2];

        MaterialPropertyBlock m_MaterialProperty = new MaterialPropertyBlock();

        public DebugLightVolumes()
        {
        }

        public void InitData(RenderPipelineResources renderPipelineResources)
        {
            m_DebugLightVolumeMaterial = CoreUtils.CreateEngineMaterial(renderPipelineResources.shaders.debugLightVolumePS);
            m_DebugLightVolumeCompute = renderPipelineResources.shaders.debugLightVolumeCS;
            m_DebugLightVolumeGradientKernel = m_DebugLightVolumeCompute.FindKernel("LightVolumeGradient");
            m_DebugLightVolumeColorsKernel = m_DebugLightVolumeCompute.FindKernel("LightVolumeColors");
            m_ColorGradientTexture = renderPipelineResources.textures.colorGradient;

            m_Blit = CoreUtils.CreateEngineMaterial(renderPipelineResources.shaders.blitPS);
        }

        public void ReleaseData()
        {
            CoreUtils.Destroy(m_Blit);
            CoreUtils.Destroy(m_DebugLightVolumeMaterial);
        }

        public void RenderLightVolumes(CommandBuffer cmd, HDCamera hdCamera, CullingResults cullResults, LightingDebugSettings lightDebugSettings, RTHandleSystem.RTHandle finalRT, RTManager rtManager)
        {
            using (new ProfilingSample(cmd, "Display Light Volumes"))
            {
                // Fill the render target array
                m_RTIDs[0] = rtManager.GetRenderTarget(RT.DebugLightVolumeCount);
                m_RTIDs[1] = rtManager.GetRenderTarget(RT.DebugLightVolumeAccumulation);

                // Clear the buffers
                HDUtils.SetRenderTarget(cmd, hdCamera, rtManager.GetRenderTarget(RT.DebugLightVolumeAccumulation), ClearFlag.Color, Color.black);
                HDUtils.SetRenderTarget(cmd, hdCamera, rtManager.GetRenderTarget(RT.DebugLightVolumeCount), ClearFlag.Color, Color.black);
                HDUtils.SetRenderTarget(cmd, hdCamera, rtManager.GetRenderTarget(RT.DebugLightVolume), ClearFlag.Color, Color.black);

                // Set the render target array
                HDUtils.SetRenderTarget(cmd, hdCamera, m_RTIDs, rtManager.GetRenderTarget(RT.DepthStencil)); // Depth is not actually needed for light volume but we need to pass one the API for proper viewport scaling

                // First of all let's do the regions for the light sources (we only support Punctual and Area)
                int numLights = cullResults.visibleLights.Length;
                for (int lightIdx = 0; lightIdx < numLights; ++lightIdx)
                {
                    // Let's build the light's bounding sphere matrix
                    Light currentLegacyLight = cullResults.visibleLights[lightIdx].light;
                    if (currentLegacyLight == null) continue;
                    HDAdditionalLightData currentHDRLight = currentLegacyLight.GetComponent<HDAdditionalLightData>();
                    if (currentHDRLight == null) continue;

                    var trsMat = currentLegacyLight.gameObject.transform.localToWorldMatrix;
                    trsMat.SetTRS(currentLegacyLight.gameObject.transform.position, currentLegacyLight.gameObject.transform.rotation, Vector3.one);

                    if (currentLegacyLight.type == LightType.Point || currentLegacyLight.type == LightType.Area)
                    {
                        m_MaterialProperty.SetVector(_RangeShaderID, new Vector3(currentLegacyLight.range, currentLegacyLight.range, currentLegacyLight.range));
                        switch (currentHDRLight.lightTypeExtent)
                        {
                            case LightTypeExtent.Punctual:
                                {
                                    m_MaterialProperty.SetColor(_ColorShaderID, new Color(0.0f, 0.5f, 0.0f, 1.0f));
                                    m_MaterialProperty.SetVector(_OffsetShaderID, new Vector3(0, 0, 0));
                                    cmd.DrawMesh(DebugShapes.instance.RequestSphereMesh(), trsMat, m_DebugLightVolumeMaterial, 0, 0, m_MaterialProperty);
                                }
                                break;
                            case LightTypeExtent.Rectangle:
                                {
                                    m_MaterialProperty.SetColor(_ColorShaderID, new Color(0.0f, 1.0f, 1.0f, 1.0f));
                                    m_MaterialProperty.SetVector(_OffsetShaderID, new Vector3(0, 0, 0));
                                    cmd.DrawMesh(DebugShapes.instance.RequestSphereMesh(), trsMat, m_DebugLightVolumeMaterial, 0, 0, m_MaterialProperty);
                                }
                                break;
                            case LightTypeExtent.Tube:
                                {
                                    m_MaterialProperty.SetColor(_ColorShaderID, new Color(1.0f, 0.0f, 0.5f, 1.0f));
                                    m_MaterialProperty.SetVector(_OffsetShaderID, new Vector3(0, 0, 0));
                                    cmd.DrawMesh(DebugShapes.instance.RequestSphereMesh(), trsMat, m_DebugLightVolumeMaterial, 0, 0, m_MaterialProperty);
                                }
                                break;
                            default:
                                break;
                        }
                    }
                    else if (currentLegacyLight.type == LightType.Spot)
                    {
                        if (currentHDRLight.spotLightShape == SpotLightShape.Cone)
                        {
                            float bottomRadius = Mathf.Tan(currentLegacyLight.spotAngle * Mathf.PI / 360.0f) * currentLegacyLight.range;
                            m_MaterialProperty.SetColor(_ColorShaderID, new Color(1.0f, 0.5f, 0.0f, 1.0f));
                            m_MaterialProperty.SetVector(_RangeShaderID, new Vector3(bottomRadius, bottomRadius, currentLegacyLight.range));
                            m_MaterialProperty.SetVector(_OffsetShaderID, new Vector3(0, 0, 0));
                            cmd.DrawMesh(DebugShapes.instance.RequestConeMesh(), trsMat, m_DebugLightVolumeMaterial, 0, 0, m_MaterialProperty);
                        }
                        else if (currentHDRLight.spotLightShape == SpotLightShape.Box)
                        {
                            m_MaterialProperty.SetColor(_ColorShaderID, new Color(1.0f, 0.5f, 0.0f, 1.0f));
                            m_MaterialProperty.SetVector(_RangeShaderID, new Vector3(currentHDRLight.shapeWidth, currentHDRLight.shapeHeight, currentLegacyLight.range));
                            m_MaterialProperty.SetVector(_OffsetShaderID, new Vector3(0, 0, currentLegacyLight.range / 2.0f));
                            cmd.DrawMesh(DebugShapes.instance.RequestBoxMesh(), trsMat, m_DebugLightVolumeMaterial, 0, 0, m_MaterialProperty);
                        }
                        else if (currentHDRLight.spotLightShape == SpotLightShape.Pyramid)
                        {
                            float bottomWidth = Mathf.Tan(currentLegacyLight.spotAngle * Mathf.PI / 360.0f) * currentLegacyLight.range;
                            m_MaterialProperty.SetColor(_ColorShaderID, new Color(1.0f, 0.5f, 0.0f, 1.0f));
                            m_MaterialProperty.SetVector(_RangeShaderID, new Vector3(currentHDRLight.aspectRatio * bottomWidth * 2, bottomWidth * 2, currentLegacyLight.range));
                            m_MaterialProperty.SetVector(_OffsetShaderID, new Vector3(0, 0, 0));
                            cmd.DrawMesh(DebugShapes.instance.RequestPyramidMesh(), trsMat, m_DebugLightVolumeMaterial, 0, 0, m_MaterialProperty);
                        }
                    }
                }

                // Now let's do the same but for reflection probes
                int numProbes = cullResults.visibleReflectionProbes.Length;
                for (int probeIdx = 0; probeIdx < numProbes; ++probeIdx)
                {
                    // Let's build the light's bounding sphere matrix
                    ReflectionProbe currentLegacyProbe = cullResults.visibleReflectionProbes[probeIdx].reflectionProbe;
                    HDAdditionalReflectionData currentHDProbe = currentLegacyProbe.GetComponent<HDAdditionalReflectionData>();

                    if (!currentHDProbe)
                        continue;

                    var trsMat = currentLegacyProbe.gameObject.transform.localToWorldMatrix;
                    trsMat.SetTRS(currentLegacyProbe.gameObject.transform.position, currentLegacyProbe.gameObject.transform.rotation, Vector3.one);

                    MaterialPropertyBlock m_MaterialProperty = new MaterialPropertyBlock();
                    Mesh targetMesh = null;
                    if (currentHDProbe.influenceVolume.shape == InfluenceShape.Sphere)
                    {
                        m_MaterialProperty.SetVector(_RangeShaderID, new Vector3(currentHDProbe.influenceVolume.sphereRadius, currentHDProbe.influenceVolume.sphereRadius, currentHDProbe.influenceVolume.sphereRadius));
                        targetMesh = DebugShapes.instance.RequestSphereMesh();
                    }
                    else
                    {
                        m_MaterialProperty.SetVector(_RangeShaderID, new Vector3(currentHDProbe.influenceVolume.boxSize.x, currentHDProbe.influenceVolume.boxSize.y, currentHDProbe.influenceVolume.boxSize.z));
                        targetMesh = DebugShapes.instance.RequestBoxMesh();
                    }

                    m_MaterialProperty.SetColor(_ColorShaderID, new Color(1.0f, 1.0f, 0.0f, 1.0f));
                    m_MaterialProperty.SetVector(_OffsetShaderID, new Vector3(0, 0, 0));
                    Matrix4x4 positionMat = Matrix4x4.Translate(currentLegacyProbe.transform.position);
                    cmd.DrawMesh(targetMesh, trsMat, m_DebugLightVolumeMaterial, 0, 0, m_MaterialProperty);
                }

                // Define which kernel to use based on the lightloop options
                int targetKernel = lightDebugSettings.lightVolumeDebugByCategory == LightLoop.LightVolumeDebug.ColorAndEdge ? m_DebugLightVolumeColorsKernel : m_DebugLightVolumeGradientKernel;

                // Set the input params for the compute
                cmd.SetComputeTextureParam(m_DebugLightVolumeCompute, targetKernel, _DebugLightCountBufferShaderID, rtManager.GetRenderTarget(RT.DebugLightVolumeCount));
                cmd.SetComputeTextureParam(m_DebugLightVolumeCompute, targetKernel, _DebugColorAccumulationBufferShaderID, rtManager.GetRenderTarget(RT.DebugLightVolumeAccumulation));
                cmd.SetComputeTextureParam(m_DebugLightVolumeCompute, targetKernel, _DebugLightVolumesTextureShaderID, rtManager.GetRenderTarget(RT.DebugLightVolume));
                cmd.SetComputeTextureParam(m_DebugLightVolumeCompute, targetKernel, _ColorGradientTextureShaderID, m_ColorGradientTexture);
                cmd.SetComputeIntParam(m_DebugLightVolumeCompute, _MaxDebugLightCountShaderID, (int)lightDebugSettings.maxDebugLightCount);

                // Texture dimensions
                int texWidth = hdCamera.actualWidth;
                int texHeight = hdCamera.actualHeight;

                // Dispatch the compute
                int lightVolumesTileSize = 8;
                int numTilesX = (texWidth + (lightVolumesTileSize - 1)) / lightVolumesTileSize;
                int numTilesY = (texHeight + (lightVolumesTileSize - 1)) / lightVolumesTileSize;
                cmd.DispatchCompute(m_DebugLightVolumeCompute, targetKernel, numTilesX, numTilesY, 1);

                // Blit this into the camera target
                HDUtils.SetRenderTarget(cmd, hdCamera, finalRT);
                m_MaterialProperty.SetTexture(HDShaderIDs._BlitTexture, rtManager.GetRenderTarget(RT.DebugLightVolume));
                cmd.DrawProcedural(Matrix4x4.identity, m_DebugLightVolumeMaterial, 1, MeshTopology.Triangles, 3, 1, m_MaterialProperty);
            }
        }
    }
}
