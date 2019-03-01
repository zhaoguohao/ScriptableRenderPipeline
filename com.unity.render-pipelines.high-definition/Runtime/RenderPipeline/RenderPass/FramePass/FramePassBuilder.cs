using System;
using System.Collections.Generic;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public class FramePassBuilder : IDisposable
    {
        // Owned
        private List<FramePassData> m_FramePassData;

        public FramePassBuilder Add(
            FramePassSettings settings,
            FramePassBufferAllocator bufferAllocator,
            Buffers[] buffers,
            FramePassCallback callback
        )
        {
            (m_FramePassData ?? (m_FramePassData = ListPool<FramePassData>.Get())).Add(
                new FramePassData(settings, bufferAllocator, buffers, callback));
            return this;
        }

        public FramePassDataCollection Build()
        {
            var result = new FramePassDataCollection(m_FramePassData);
            m_FramePassData = null;
            return result;
        }

        public void Dispose()
        {
            if (m_FramePassData == null) return;
            ListPool<FramePassData>.Release(m_FramePassData);
            m_FramePassData = null;
        }
    }
}
