using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public class DBufferManager
    {
        public bool enableDecals { get; set; }

        public void ClearAndSetTargets(CommandBuffer cmd, HDCamera camera, bool rtCount4, RTManager rtManager)
        {
            // for alpha compositing, color is cleared to 0, alpha to 1
            // https://developer.nvidia.com/gpugems/GPUGems3/gpugems3_ch23.html

            // this clears the targets
            Color clearColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);
            Color clearColorNormal = new Color(0.5f, 0.5f, 0.5f, 1.0f); // for normals 0.5 is neutral
            Color clearColorAOSBlend = new Color(1.0f, 1.0f, 1.0f, 1.0f);
            HDUtils.SetRenderTarget(cmd, camera, rtManager.GetRenderTarget(RT.DBuffer0), ClearFlag.Color, clearColor);
            HDUtils.SetRenderTarget(cmd, camera, rtManager.GetRenderTarget(RT.DBuffer1), ClearFlag.Color, clearColorNormal);
            HDUtils.SetRenderTarget(cmd, camera, rtManager.GetRenderTarget(RT.DBuffer2), ClearFlag.Color, clearColor);

            if (rtCount4)
            {
                HDUtils.SetRenderTarget(cmd, camera, rtManager.GetRenderTarget(RT.DBuffer3), ClearFlag.Color, clearColorAOSBlend);
            }
            HDUtils.SetRenderTarget(cmd, camera, rtManager.GetRenderTarget(RT.DBufferHTile), ClearFlag.Color, CoreUtils.clearColorAllBlack);

            // this actually sets the MRTs and HTile RWTexture, this is done separately because we do not have an api to clear MRTs to different colors
            HDUtils.SetRenderTarget(cmd, camera, rtManager.GetDBufferRTI(), rtManager.GetDepthStencilBuffer()); // do not clear anymore
            cmd.SetRandomWriteTarget(rtCount4 ? 4 : 3, rtManager.GetRenderTarget(RT.DBufferHTile));
        }

        public void UnSetHTile(CommandBuffer cmd)
        {
            cmd.ClearRandomWriteTargets();
        }

        public void SetHTileTexture(CommandBuffer cmd, RTManager rtManager)
        {
            cmd.SetGlobalTexture(HDShaderIDs._DecalHTileTexture, rtManager.GetRenderTarget(RT.DBufferHTile));
        }

        public void PushGlobalParams(HDCamera hdCamera, CommandBuffer cmd, bool rtCount4, RTManager rtManager)
        {
            int bufferCount = rtCount4 ? 4 : 3;
            if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.Decals))
            {
                cmd.SetGlobalInt(HDShaderIDs._EnableDecals, enableDecals ? 1 : 0);
                cmd.SetGlobalVector(HDShaderIDs._DecalAtlasResolution, new Vector2(HDUtils.hdrpSettings.decalSettings.atlasWidth, HDUtils.hdrpSettings.decalSettings.atlasHeight));
                for (int i = 0; i < bufferCount; ++i)
                {
                    cmd.SetGlobalTexture(HDShaderIDs._DBufferTexture[i], rtManager.GetRenderTarget(RT.DBuffer0 + i));
                }
            }
            else
            {
                cmd.SetGlobalInt(HDShaderIDs._EnableDecals, 0);
                // We still bind black textures to make sure that something is bound (can be a problem on some platforms)
                for (int i = 0; i < bufferCount; ++i)
                {
                    cmd.SetGlobalTexture(HDShaderIDs._DBufferTexture[i], Texture2D.blackTexture);
                }
            }
        }
    }
}
