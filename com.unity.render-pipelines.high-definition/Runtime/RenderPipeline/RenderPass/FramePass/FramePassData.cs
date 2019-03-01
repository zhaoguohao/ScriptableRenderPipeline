using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public delegate void FramePassCallback(CommandBuffer cmd, List<RTHandleSystem.RTHandle> buffers, FrameProperties properties);
    public delegate RTHandleSystem.RTHandle FramePassBufferAllocator(Buffers bufferId);

    public struct FramePassData
    {
        public static readonly FramePassData @default = new FramePassData
        {
            m_Settings = FramePassSettings.@default,
            m_RequestedBuffers = new Buffers[] {},
            m_Callback = null
        };

        private FramePassSettings m_Settings;
        private Buffers[] m_RequestedBuffers;
        private FramePassCallback m_Callback;
        private readonly FramePassBufferAllocator m_BufferAllocator;

        public bool isValid => m_RequestedBuffers != null && m_Callback != null;

        public FramePassData(
            FramePassSettings settings,
            FramePassBufferAllocator bufferAllocator,
            Buffers[] requestedBuffers,
            FramePassCallback callback
        )
        {
            m_Settings = settings;
            m_BufferAllocator = bufferAllocator;
            m_RequestedBuffers = requestedBuffers;
            m_Callback = callback;
        }

        public void AllocateTargetTexturesIfRequired(ref List<RTHandleSystem.RTHandle> textures)
        {
            if (!isValid || textures == null)
                return;

            Assert.IsNotNull(m_RequestedBuffers);

            textures.Clear();

            foreach (var bufferId in m_RequestedBuffers)
                textures.Add(m_BufferAllocator(bufferId));
        }

        public void PushCameraTexture(
            CommandBuffer cmd,
            Buffers bufferId,
            HDCamera camera,
            RTHandleSystem.RTHandle target,
            List<RTHandleSystem.RTHandle> targets
        )
        {
            if (!isValid)
                return;

            Assert.IsNotNull(m_RequestedBuffers);
            Assert.IsNotNull(targets);

            var index = Array.IndexOf(m_RequestedBuffers, bufferId);
            if (index == -1)
                return;

            HDUtils.BlitCameraTexture(cmd, camera, target, targets[index]);
        }

        public void Execute(CommandBuffer cmd, List<RTHandleSystem.RTHandle> framePassTextures, FrameProperties properties)
        {
            if (!isValid)
                return;

            m_Callback(cmd, framePassTextures, properties);
        }

        public void SetupDebugData(ref DebugDisplaySettings debugDisplaySettings)
        {
            if (!isValid)
                return;

            debugDisplaySettings = new DebugDisplaySettings();
            m_Settings.FillDebugData(debugDisplaySettings.data);
        }
    }
}
