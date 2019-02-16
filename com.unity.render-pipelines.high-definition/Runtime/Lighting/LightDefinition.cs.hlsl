//
// This file was automatically generated. Please don't edit by hand.
//

#ifndef LIGHTDEFINITION_CS_HLSL
#define LIGHTDEFINITION_CS_HLSL
//
// UnityEngine.Experimental.Rendering.HDPipeline.GPULightType:  static fields
//
#define GPULIGHTTYPE_DIRECTIONAL (0)
#define GPULIGHTTYPE_POINT (1)
#define GPULIGHTTYPE_SPOT (2)
#define GPULIGHTTYPE_PROJECTOR_PYRAMID (3)
#define GPULIGHTTYPE_PROJECTOR_BOX (4)
#define GPULIGHTTYPE_TUBE (5)
#define GPULIGHTTYPE_RECTANGLE (6)

//
// UnityEngine.Experimental.Rendering.HDPipeline.GPUImageBasedLightingType:  static fields
//
#define GPUIMAGEBASEDLIGHTINGTYPE_REFLECTION (0)
#define GPUIMAGEBASEDLIGHTINGTYPE_REFRACTION (1)

//
// UnityEngine.Experimental.Rendering.HDPipeline.DirectionalLightData:  static fields
//
#define DEBUGVIEW_DIRECTIONALLIGHTDATA_MIN_ROUGHNESS (1)
#define DEBUGVIEW_DIRECTIONALLIGHTDATA_SHADOW_MASK_SELECTOR (2)

//
// UnityEngine.Experimental.Rendering.HDPipeline.LightData:  static fields
//
#define DEBUGVIEW_LIGHTDATA_ANGLE_SCALE (1)
#define DEBUGVIEW_LIGHTDATA_ANGLE_OFFSET (2)
#define DEBUGVIEW_LIGHTDATA_RANGE (3)
#define DEBUGVIEW_LIGHTDATA_MIN_ROUGHNESS (4)
#define DEBUGVIEW_LIGHTDATA_SHADOW_MASK_SELECTOR (5)
#define DEBUGVIEW_LIGHTDATA_SIZE (6)

//
// UnityEngine.Experimental.Rendering.HDPipeline.EnvShapeType:  static fields
//
#define ENVSHAPETYPE_NONE (0)
#define ENVSHAPETYPE_BOX (1)
#define ENVSHAPETYPE_SPHERE (2)
#define ENVSHAPETYPE_SKY (3)

//
// UnityEngine.Experimental.Rendering.HDPipeline.EnvConstants:  static fields
//
#define ENVCONSTANTS_SPEC_CUBE_LOD_STEP (6)

//
// UnityEngine.Experimental.Rendering.HDPipeline.EnvLightData:  static fields
//
#define DEBUGVIEW_ENVLIGHTDATA_MIN_PROJECTION_DISTANCE (1)
#define DEBUGVIEW_ENVLIGHTDATA_BOX_SIDE_FADE_POSITIVE (2)
#define DEBUGVIEW_ENVLIGHTDATA_BOX_SIDE_FADE_NEGATIVE (3)

//
// UnityEngine.Experimental.Rendering.HDPipeline.EnvCacheType:  static fields
//
#define ENVCACHETYPE_TEXTURE2D (0)
#define ENVCACHETYPE_CUBEMAP (1)

//
// UnityEngine.Experimental.Rendering.HDPipeline.StencilLightingUsage:  static fields
//
#define STENCILLIGHTINGUSAGE_NO_LIGHTING (0)
#define STENCILLIGHTINGUSAGE_SPLIT_LIGHTING (1)
#define STENCILLIGHTINGUSAGE_REGULAR_LIGHTING (2)

// Generated from UnityEngine.Experimental.Rendering.HDPipeline.DirectionalLightData
// PackingRules = Exact
struct DirectionalLightData
{
    float3 positionRWS;
    uint lightLayers;
    float lightDimmer;
    float volumetricLightDimmer;
    float angleScale;
    float angleOffset;
    float3 forward;
    int cookieIndex;
    float3 right;
    int tileCookie;
    float3 up;
    int shadowIndex;
    float3 color;
    int contactShadowIndex;
    float shadowDimmer;
    float volumetricShadowDimmer;
    int nonLightMappedOnly;
    real minRoughness;
    real4 shadowMaskSelector;
    float diffuseDimmer;
    float specularDimmer;
};

// Generated from UnityEngine.Experimental.Rendering.HDPipeline.LightData
// PackingRules = Exact
struct LightData
{
    float3 positionRWS;
    uint lightLayers;
    float lightDimmer;
    float volumetricLightDimmer;
    real angleScale;
    real angleOffset;
    float3 forward;
    int lightType;
    float3 right;
    real range;
    float3 up;
    float rangeAttenuationScale;
    float3 color;
    float rangeAttenuationBias;
    int cookieIndex;
    int tileCookie;
    int shadowIndex;
    int contactShadowIndex;
    float shadowDimmer;
    float volumetricShadowDimmer;
    int nonLightMappedOnly;
    real minRoughness;
    real4 shadowMaskSelector;
    real2 size;
    float diffuseDimmer;
    float specularDimmer;
};

// Generated from UnityEngine.Experimental.Rendering.HDPipeline.EnvLightData
// PackingRules = Exact
struct EnvLightData
{
    uint lightLayers;
    float3 capturePositionRWS;
    int influenceShapeType;
    float3 proxyExtents;
    real minProjectionDistance;
    float3 proxyPositionRWS;
    float3 proxyForward;
    float3 proxyUp;
    float3 proxyRight;
    float3 influencePositionRWS;
    float3 influenceForward;
    float3 influenceUp;
    float3 influenceRight;
    float3 influenceExtents;
    float unused00;
    float3 blendDistancePositive;
    float3 blendDistanceNegative;
    float3 blendNormalDistancePositive;
    float3 blendNormalDistanceNegative;
    real3 boxSideFadePositive;
    real3 boxSideFadeNegative;
    float weight;
    float multiplier;
    int envIndex;
};


#endif
