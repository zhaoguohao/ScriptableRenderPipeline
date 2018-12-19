#ifdef HAS_LIGHTLOOP
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/Raytracing/Shaders/RaytracingLightLoop.hlsl"
#endif

// Generic function that handles the reflection code
[shader("closesthit")]
void ClosestHitMain(inout RayIntersection rayIntersection : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
{
	// The first thing that we should do is grab the intersection vertice
    IntersectionVertice currentvertex;
    CurrentIntersectionVertice(attributeData, currentvertex);

    // Build the Frag inputs from the intersection vertice
    FragInputs fragInput;
    BuildFragInputsFromIntersection(currentvertex, fragInput);

    // Compute the view vector
    float3 viewWS = -rayIntersection.incidentDirection;

    // Make sure to add the additional travel distance
    float travelDistance = length(fragInput.positionRWS + _WorldSpaceCameraPos - rayIntersection.origin);
    rayIntersection.t = travelDistance;
    rayIntersection.cone.width += travelDistance * rayIntersection.cone.spreadAngle;
    
    // Build the surfacedata and builtindata
    SurfaceData surfaceData;
    BuiltinData builtinData;
    GetSurfaceDataFromIntersection(fragInput, viewWS, currentvertex, rayIntersection.cone, surfaceData, builtinData);

    PositionInputs posInput;
    posInput.positionWS = fragInput.positionRWS;

    // Compute the bsdf data
    BSDFData bsdfData =  ConvertSurfaceDataToBSDFData(uint2(0, 0), surfaceData);

#ifdef HAS_LIGHTLOOP
    // Compute the prelight data
    PreLightData preLightData = GetPreLightData(viewWS, posInput, bsdfData);

    builtinData.bakeDiffuseLighting *= (preLightData.diffuseFGD * bsdfData.diffuseColor);

    // Run the lightloop
    float3 diffuseLighting;
    float3 specularLighting;
    LightLoop(viewWS, posInput, preLightData, bsdfData, builtinData, diffuseLighting, specularLighting);

    // Color display for the moment
    rayIntersection.color = diffuseLighting + specularLighting;
#else
    rayIntersection.color = bsdfData.color;
#endif
}