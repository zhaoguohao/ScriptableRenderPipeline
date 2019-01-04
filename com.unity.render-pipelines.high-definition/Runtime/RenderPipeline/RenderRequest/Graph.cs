using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    internal static class Graph
    {
        public struct NodeID : IEquatable<NodeID>
        {
            int m_Index;

            internal NodeID(int i) => m_Index = i;

            public static implicit operator NodeID(int i) => new NodeID(i);
            public static implicit operator int(NodeID n) => n.m_Index;
            public bool Equals(NodeID other) => m_Index == other.m_Index;
        }
    }

    /// <summary>Generic graph that can be allocated on the stack.</summary>
    /// <typeparam name="TNode"></typeparam>
    internal unsafe struct Graph<TNode>
        where TNode: struct
    {
        struct MemoryLayout
        {
            public int NodesOffset;
            public int NodeDependsOnCountOffset;
            public int NodeDependsOnOffset;

            public MemoryLayout(int maxNodes, int maxLinkPerNode)
            {
                NodesOffset = 0;
                NodeDependsOnCountOffset = maxNodes * k_NodeSize;
                NodeDependsOnOffset = NodeDependsOnCountOffset + sizeof(int) * maxNodes;
            }
        }

        // JIT Cache
        static readonly int k_NodeSize = UnsafeUtility.SizeOf<TNode>();

        // Buffer Layout
        // TNode m_Nodes[m_MaxNode];
        // int m_NodeDependsOnCount[m_MaxNode];
        // int m_NodeDependsOn[m_MaxNode * m_MaxLinkPerNode];
        void* m_Buffer;

        MemoryLayout m_MemoryLayout;
        int m_MaxNode;
        int m_MaxLinkPerNode;
        int m_NodeCount;

        public int NodeCount => m_NodeCount;

        public TNode this[Graph.NodeID id]
        {
            get
            {
                if (id >= m_NodeCount)
                    throw new ArgumentOutOfRangeException(nameof(id));
                return UnsafeUtility.ReadArrayElement<TNode>(nodesPtr, (int)id);
            }
            set
            {
                if (id >= m_NodeCount)
                    throw new ArgumentOutOfRangeException(nameof(id));
                UnsafeUtility.WriteArrayElement(nodesPtr, (int)id, value);
            }
        }

        public Graph(void* buffer, int bufferLength, int maxNodes, int maxLinkPerNode)
        {
            var requiredSize = SizeFor(maxNodes, maxLinkPerNode);
            if (bufferLength < requiredSize)
            {
                throw new ArgumentException(
                    $"Buffer provided must be at least of {requiredSize} byte, but provided {bufferLength} bytes."
                );
            }

            m_MemoryLayout = new MemoryLayout(maxNodes, maxLinkPerNode);
            m_Buffer = buffer;
            m_MaxLinkPerNode = maxLinkPerNode;
            m_MaxNode = maxNodes;
            m_NodeCount = 0;

            UnsafeUtility.MemClear(m_Buffer, bufferLength);
        }

        public Graph.NodeID AddNode(ref TNode node)
        {
            if (m_NodeCount >= m_MaxNode)
            {
                throw new InvalidOperationException(
                    $"Trying to add a node while maximum count ({m_MaxNode}) was reached."
                );
            }

            UnsafeUtility.WriteArrayElement(nodesPtr, m_NodeCount, node);
            var id = m_NodeCount;
            ++m_NodeCount;
            return id;
        }

        public void AddNodeDependency(Graph.NodeID nodeIndex, Graph.NodeID dependsOn)
        {
            ref var nodeLinkCount = ref *GetNodeDependsOnCountPtr(nodeIndex);
            if (nodeLinkCount >= m_MaxLinkPerNode)
            {
                throw new InvalidOperationException(
                    $"Trying to add a node link while maximum count ({m_MaxLinkPerNode}) " +
                    $"was reached for node {nodeIndex}."
                );
            }

            *GetNodeDependsOnPtr(nodeIndex, nodeLinkCount) = dependsOn;
            ++nodeLinkCount;
        }

        public int ComputeLinearExecutionOrder(Graph.NodeID* ids, int idsLength)
        {
            if (idsLength < m_NodeCount)
                throw new ArgumentException(
                    $"Expected to have a buffer of length greater ore equals to {m_NodeCount}, but received {idsLength}."
                );

            // find roots
            var rootIDsPtr = stackalloc Graph.NodeID[m_NodeCount];
            var rootIDsLength = 0;
            for (var i = 0; i < m_NodeCount; i++)
            {
                ref var nodeDependencies = ref *GetNodeDependsOnCountPtr(i);
                if (nodeDependencies != 0)
                    continue;

                rootIDsPtr[rootIDsLength] = i;
                ++rootIDsLength;
            }

            var currentIDsLength = 0;
            using (GenericPool<Stack<Graph.NodeID>>.Get(out var stack))
            {
                stack.Clear();
                for (var i = 0; i < rootIDsLength; ++i)
                {
                    stack.Push(rootIDsPtr[i]);
                    while (stack.Count > 0)
                    {
                        var index = stack.Pop();
                        if (CoreUnsafeUtils.IndexOf(ids, currentIDsLength, index) == -1)
                        {
                            ids[currentIDsLength] = index;
                            ++currentIDsLength;
                        }

                        ref var requestDependsOnCount = ref *GetNodeDependsOnCountPtr(index);
                        var requestDependsOnArray = GetNodeDependsOnPtr(index, 0);
                        for (var j = 0; j < requestDependsOnCount; ++j)
                            stack.Push(requestDependsOnArray[j]);
                    }
                }
            }

            return currentIDsLength;
        }

        internal void* GetNodePtr(Graph.NodeID nodeId)
        {
            if (nodeId > m_NodeCount)
                throw new ArgumentOutOfRangeException(nameof(nodeId));

            return (byte*)nodesPtr + k_NodeSize * nodeId;
        }

        void* nodesPtr => m_Buffer;
        int* nodeDependsOnCountPtr => (int*)((byte*)m_Buffer + m_MemoryLayout.NodeDependsOnCountOffset);
        int* nodeDependsOnPtr => (int*)((byte*)m_Buffer + m_MemoryLayout.NodeDependsOnOffset);

        int* GetNodeDependsOnCountPtr(int nodeIndex) => nodeDependsOnCountPtr + nodeIndex;
        int* GetNodeDependsOnPtr(int nodeIndex, int linkIndex)
            => nodeDependsOnPtr + nodeIndex * m_MaxLinkPerNode + linkIndex;

        public static int SizeFor(int maxNodes, int maxLinkPerNode)
            => sizeof(int) * maxLinkPerNode * maxNodes      // m_NodeDependsOn
                + sizeof(int) * maxNodes                    // m_NodeDependsOnCount
                + k_NodeSize * maxNodes;                    // m_Nodes
    }
}
