
namespace UnityEngine.Experimental.VoxelizedShadowMaps
{
    public sealed class SpotVxShadowMap : VxShadowMap
    {
        // TODO :
        public VoxelResolution voxelResolution = VoxelResolution._4096;
        public int voxelResolutionInt => (int)voxelResolution;
        public override VoxelResolution subtreeResolution => VoxelResolution._4096;
    }
}
