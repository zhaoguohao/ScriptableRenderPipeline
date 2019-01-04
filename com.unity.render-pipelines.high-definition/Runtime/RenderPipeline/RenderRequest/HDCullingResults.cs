using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    struct HDCullingResults
    {
        public CullingResults cullingResults;
        public HDProbeCullingResults hdProbeCullingResults;
        // TODO: DecalCullResults

        internal void Clear() => hdProbeCullingResults.Reset();
    }
}
