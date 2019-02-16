using System;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public partial class Lit : RenderPipelineMaterial
    {
        // Currently we have only one materialId (Standard GGX), so it is not store in the GBuffer and we don't test for it

        // If change, be sure it match what is done in Lit.hlsl: MaterialFeatureFlagsFromGBuffer
        // Material bit mask must match the size define LightDefinitions.s_MaterialFeatureMaskFlags value
        [GenerateHLSL(PackingRules.Exact)]
        public enum MaterialFeatureFlags
        {
            LitStandard             = 1 << 0,   // For material classification we need to identify that we are indeed use as standard material, else we are consider as sky/background element
            LitSpecularColor        = 1 << 1,   // LitSpecularColor is not use statically but only dynamically
            LitSubsurfaceScattering = 1 << 2,
            LitTransmission         = 1 << 3,
            LitAnisotropy           = 1 << 4,
            LitIridescence          = 1 << 5,
            LitClearCoat            = 1 << 6
        };

        //-----------------------------------------------------------------------------
        // SurfaceData
        //-----------------------------------------------------------------------------

        // Main structure that store the user data (i.e user input of master node in material graph)
        [GenerateHLSL(PackingRules.Exact, false, false, true, 1000, false, true)]
        public struct SurfaceData
        {
            [SurfaceDataAttributes("MaterialFeatures")]
            public uint materialFeatures;

            // Standard
            [SurfaceDataAttributes("Base Color", false, true)]
            [PackingAttribute("baseColor", FieldPacking.Real)]
            public Vector3 baseColor;
            [SurfaceDataAttributes("Specular Occlusion")]
            [PackingAttribute("specularOcclusion", FieldPacking.Real)]
            public float specularOcclusion;

            [SurfaceDataAttributes(new string[] {"Normal", "Normal View Space"}, true)]
            public Vector3 normalWS;
            [SurfaceDataAttributes("Smoothness")]
            [PackingAttribute("perceptualSmoothness", FieldPacking.Real)]
            public float perceptualSmoothness;

            [SurfaceDataAttributes("Ambient Occlusion")]
            [PackingAttribute("ambientOcclusion", FieldPacking.Real)]
            public float ambientOcclusion;

            [SurfaceDataAttributes("Metallic")]
            [PackingAttribute("metallic", FieldPacking.Real)]
            public float metallic;

            [SurfaceDataAttributes("Coat mask")]
            [PackingAttribute("coatMask", FieldPacking.Real)]
            public float coatMask;

            // MaterialFeature dependent attribute

            // Specular Color
            [SurfaceDataAttributes("Specular Color", false, true)]
            [PackingAttribute("specularColor", FieldPacking.Real)]
            public Vector3 specularColor;

            // SSS
            [SurfaceDataAttributes("Diffusion Profile")]
            public uint diffusionProfile;
            [SurfaceDataAttributes("Subsurface Mask")]
            [PackingAttribute("subsurfaceMask", FieldPacking.Real)]
            public float subsurfaceMask;

            // Transmission
            // + Diffusion Profile
            [SurfaceDataAttributes("Thickness")]
            [PackingAttribute("thickness", FieldPacking.Real)]
            public float thickness;

            // Anisotropic
            [SurfaceDataAttributes("Tangent", true)]
            public Vector3 tangentWS;
            [SurfaceDataAttributes("Anisotropy")]
            [PackingAttribute("anisotropy", FieldPacking.Real)]
            public float anisotropy; // anisotropic ratio(0->no isotropic; 1->full anisotropy in tangent direction, -1->full anisotropy in bitangent direction)

            // Iridescence
            [SurfaceDataAttributes("Iridescence Layer Thickness")]
            [PackingAttribute("iridescenceThickness", FieldPacking.Real)]
            public float iridescenceThickness;
            [SurfaceDataAttributes("Iridescence Mask")]
            [PackingAttribute("iridescenceMask", FieldPacking.Real)]
            public float iridescenceMask;

            // Forward property only
            [SurfaceDataAttributes(new string[] { "Geometric Normal", "Geometric Normal View Space" }, true)]
            public Vector3 geomNormalWS;

            // Transparency
            // Reuse thickness from SSS

            [SurfaceDataAttributes("Index of refraction")]
            [PackingAttribute("ior", FieldPacking.Real)]
            public float ior;
            [SurfaceDataAttributes("Transmittance Color")]
            [PackingAttribute("transmittanceColor", FieldPacking.Real)]
            public Vector3 transmittanceColor;
            [SurfaceDataAttributes("Transmittance Absorption Distance")]
            [PackingAttribute("atDistance", FieldPacking.Real)]
            public float atDistance;
            [SurfaceDataAttributes("Transmittance mask")]
            [PackingAttribute("transmittanceMask", FieldPacking.Real)]
            public float transmittanceMask;
        };

        //-----------------------------------------------------------------------------
        // BSDFData
        //-----------------------------------------------------------------------------

        [GenerateHLSL(PackingRules.Exact, false, false, true, 1050, false, true)]
        public struct BSDFData
        {
            public uint materialFeatures;

            [SurfaceDataAttributes("", false, true)]
            [PackingAttribute("diffuseColor", FieldPacking.Real)]
            public Vector3 diffuseColor;
            [PackingAttribute("fresnel0", FieldPacking.Real)]
            public Vector3 fresnel0;

            [PackingAttribute("ambientOcclusion", FieldPacking.Real)]
            public float ambientOcclusion; // Caution: This is accessible only if light layer is enabled, otherwise it is 1
            [PackingAttribute("specularOcclusion", FieldPacking.Real)]
            public float specularOcclusion;

            [SurfaceDataAttributes(new string[] { "Normal WS", "Normal View Space" }, true)]
            public Vector3 normalWS;
            [PackingAttribute("perceptualRoughness", FieldPacking.Real)]
            public float perceptualRoughness;

            [PackingAttribute("coatMask", FieldPacking.Real)]
            public float coatMask;

            // MaterialFeature dependent attribute

            // SpecularColor fold into fresnel0

            // SSS
            public uint diffusionProfile;
            [PackingAttribute("subsurfaceMask", FieldPacking.Real)]
            public float subsurfaceMask;

            // Transmission
            // + Diffusion Profile
            [PackingAttribute("thickness", FieldPacking.Real)]
            public float thickness;
            public bool useThickObjectMode; // Read from the diffusion profile
            [PackingAttribute("transmittance", FieldPacking.Real)]
            public Vector3 transmittance;   // Precomputation of transmittance

            // Anisotropic
            [SurfaceDataAttributes("", true)]
            public Vector3 tangentWS;
            [SurfaceDataAttributes("", true)]
            public Vector3 bitangentWS;
            [PackingAttribute("roughnessT", FieldPacking.Real)]
            public float roughnessT;
            [PackingAttribute("roughnessB", FieldPacking.Real)]
            public float roughnessB;
            [PackingAttribute("anisotropy", FieldPacking.Real)]
            public float anisotropy;

            // Iridescence
            [PackingAttribute("iridescenceThickness", FieldPacking.Real)]
            public float iridescenceThickness;
            [PackingAttribute("iridescenceMask", FieldPacking.Real)]
            public float iridescenceMask;

            // ClearCoat
            [PackingAttribute("coatRoughness", FieldPacking.Real)]
            public float coatRoughness; // Automatically fill

            // Forward property only
            [SurfaceDataAttributes(new string[] { "Geometric Normal", "Geometric Normal View Space" }, true)]
            [PackingAttribute("geomNormalWS", FieldPacking.Real)]
            public Vector3 geomNormalWS;

            // Transparency
            [PackingAttribute("ior", FieldPacking.Real)]
            public float ior;
            // Reuse thickness from SSS
            [PackingAttribute("absorptionCoefficient", FieldPacking.Real)]
            public Vector3 absorptionCoefficient;
            [PackingAttribute("transmittanceMask", FieldPacking.Real)]
            public float transmittanceMask;
        };

        //-----------------------------------------------------------------------------
        // GBuffer management
        //-----------------------------------------------------------------------------

        public override bool IsDefferedMaterial() { return true; }

        protected void GetGBufferOptions(HDRenderPipelineAsset asset, out int gBufferCount, out bool supportShadowMask, out bool supportLightLayers)
        {
            // Caution: This must be in sync with GBUFFERMATERIAL_COUNT definition in 
            supportShadowMask = asset.currentPlatformRenderPipelineSettings.supportShadowMask;
            supportLightLayers = asset.currentPlatformRenderPipelineSettings.supportLightLayers;
            gBufferCount = 4 + (supportShadowMask ? 1 : 0) + (supportLightLayers ? 1 : 0);
        }

        // This must return the number of GBuffer to allocate
        public override int GetMaterialGBufferCount(HDRenderPipelineAsset asset)
        {
            int gBufferCount;
            bool unused0;
            bool unused1;
            GetGBufferOptions(asset, out gBufferCount, out unused0, out unused1);

            return gBufferCount;
        }

        public override void GetMaterialGBufferDescription(HDRenderPipelineAsset asset, out GraphicsFormat[] RTFormat, out GBufferUsage[] gBufferUsage, out bool[] enableWrite)
        {
            int gBufferCount;
            bool supportShadowMask;
            bool supportLightLayers;
            GetGBufferOptions(asset, out gBufferCount, out supportShadowMask, out supportLightLayers);

            RTFormat = new GraphicsFormat[gBufferCount];
            gBufferUsage = new GBufferUsage[gBufferCount];
            enableWrite = new bool[gBufferCount];

            RTFormat[0] = GraphicsFormat.R8G8B8A8_SRGB; // Albedo sRGB / SSSBuffer
            gBufferUsage[0] = GBufferUsage.SubsurfaceScattering;
            enableWrite[0] = false;
            RTFormat[1] = GraphicsFormat.R8G8B8A8_UNorm; // Normal Buffer
            gBufferUsage[1] = GBufferUsage.Normal;
            enableWrite[1] = true;                    // normal buffer is used as RWTexture to composite decals in forward
            RTFormat[2] = GraphicsFormat.R8G8B8A8_UNorm; // Data
            gBufferUsage[2] = GBufferUsage.None;
            enableWrite[2] = false;
            RTFormat[3] = Builtin.GetLightingBufferFormat();
            gBufferUsage[3] = GBufferUsage.None;
            enableWrite[3] = false;

            int index = 4;

            if (supportLightLayers)
            {
                RTFormat[index] = GraphicsFormat.R8G8B8A8_UNorm;
                gBufferUsage[index] = GBufferUsage.LightLayers;
                index++;
            }

            // All buffer above are fixed. However shadow mask buffer can be setup or not depends on light in view.
            // Thus it need to be the last one, so all indexes stay the same
            if (supportShadowMask)
            {
                RTFormat[index] = Builtin.GetShadowMaskBufferFormat();
                gBufferUsage[index] = GBufferUsage.ShadowMask;
                index++;
            }
        }


        //-----------------------------------------------------------------------------
        // Init precomputed texture
        //-----------------------------------------------------------------------------

        public Lit() {}

        public override void Build(HDRenderPipelineAsset hdAsset)
        {
            PreIntegratedFGD.instance.Build(PreIntegratedFGD.FGDIndex.FGD_GGXAndDisneyDiffuse);
            LTCAreaLight.instance.Build();
        }

        public override void Cleanup()
        {
            PreIntegratedFGD.instance.Cleanup(PreIntegratedFGD.FGDIndex.FGD_GGXAndDisneyDiffuse);
            LTCAreaLight.instance.Cleanup();
        }

        public override void RenderInit(CommandBuffer cmd)
        {
            PreIntegratedFGD.instance.RenderInit(PreIntegratedFGD.FGDIndex.FGD_GGXAndDisneyDiffuse, cmd);
        }

        public override void Bind(CommandBuffer cmd)
        {
            PreIntegratedFGD.instance.Bind(cmd, PreIntegratedFGD.FGDIndex.FGD_GGXAndDisneyDiffuse);
            LTCAreaLight.instance.Bind(cmd);
        }
    }
}
