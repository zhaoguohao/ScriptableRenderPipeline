
namespace UnityEngine.Experimental.VoxelizedShadowMaps
{
    public enum VoxelResolution
    {
        // for debugging
        _64 = 64,
        _128 = _64 * 2,
        _256 = _128 * 2,
        _512 = _256 * 2,

        // actually exposed in editor
        _1024 = 1024,
        _2048 = _1024 * 2,
        _4096 = _2048 * 2,
        _8192 = _4096 * 2,
        _16384 = _8192 * 2,
        _32768 = _16384 * 2,
        _65536 = _32768 * 2,
    }

    public enum DepthBits
    {
        _8 = 8,
        _16 = 16,
        //_32 = 32,
    }

    public abstract class VxShadowMap : MonoBehaviour
    {
        public static readonly VoxelResolution subTreeResolution = VoxelResolution._4096;
        public static readonly int subTreeResolutionInt = (int)subTreeResolution;
    }
}
