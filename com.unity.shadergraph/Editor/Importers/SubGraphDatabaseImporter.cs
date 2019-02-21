using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.Experimental.AssetImporters;
using UnityEditor.Graphing;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    [ScriptedImporter(2, ".sgsubgraphdb", 1)]
    class SubGraphDatabaseImporter : ScriptedImporter
    {
        public const string path = "Packages/com.unity.shadergraph/Editor/Importers/_.sgsubgraphdb";

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var currentTime = DateTime.Now.Ticks;
            if (ctx.assetPath != path)
            {
                ctx.LogImportError("The sgpostsubgraph extension may only be used internally by Shader Graph.");
                return;
            }

            if (SubGraphDatabase.instance == null)
            {
                SubGraphDatabase.instance = ScriptableObject.CreateInstance<SubGraphDatabase>();
            }
            var database = SubGraphDatabase.instance;

            var allSubGraphGuids = AssetDatabase.FindAssets($"t:{nameof(SubGraphAsset)}").ToList();
            allSubGraphGuids.Sort();
            var subGraphMap = new Dictionary<string, SubGraphData>();
            var graphMap = new Dictionary<string, GraphData>();

            foreach (var subGraphData in database.subGraphs)
            {
                if (allSubGraphGuids.BinarySearch(subGraphData.assetGuid) >= 0)
                {
                    subGraphMap.Add(subGraphData.assetGuid, subGraphData);
                }
            }
            
            var dirtySubGraphAssetGuids = new List<string>();

            foreach (var subGraphGuid in allSubGraphGuids)
            {
                var subGraphAsset = AssetDatabase.LoadAssetAtPath<SubGraphAsset>(AssetDatabase.GUIDToAssetPath(subGraphGuid));
                if (!subGraphMap.TryGetValue(subGraphGuid, out var subGraphData))
                {
                    subGraphData = new SubGraphData();
                }
                
                if (subGraphAsset.importedAt > subGraphData.processedAt)
                {
                    subGraphData.processedAt = currentTime;
                    dirtySubGraphAssetGuids.Add(subGraphGuid);
                    var subGraphPath = AssetDatabase.GUIDToAssetPath(subGraphGuid);
                    var textGraph = File.ReadAllText(subGraphPath, Encoding.UTF8);
                    var graphData = new GraphData { isSubGraph = true };
                    JsonUtility.FromJsonOverwrite(textGraph, graphData);
                    subGraphData.subGraphGuids.Clear();
                    subGraphData.subGraphGuids.AddRange(graphData.GetNodes<SubGraphNode>().Select(x => x.subGraphGuid));
                    subGraphData.subGraphGuids.Sort();
                    subGraphData.assetGuid = subGraphGuid;
                    subGraphMap[subGraphGuid] = subGraphData;
                    graphMap[subGraphGuid] = graphData;
                }
            }

            database.subGraphs = subGraphMap.Values.ToList();
            
            var dependencyMap = new Dictionary<string, List<SubGraphData>>();

            foreach (var subGraphData in database.subGraphs)
            {
                dependencyMap[subGraphData.assetGuid] = new List<SubGraphData>();
            }

            foreach (var subGraphData in database.subGraphs)
            {
                foreach (var dependencyGuid in subGraphData.subGraphGuids)
                {
                    dependencyMap[dependencyGuid].Add(subGraphData);
                }
            }

            var subGraphDatasToBeProcessed = new List<SubGraphData>();
            var wavefront = new Stack<SubGraphData>();
            foreach (var subGraphAssetGuid in dirtySubGraphAssetGuids)
            {
                var subGraphData = subGraphMap[subGraphAssetGuid];
                wavefront.Push(subGraphData);
            }
            var subGraphDepths = new Dictionary<string,int>(dirtySubGraphAssetGuids.Count);
            dirtySubGraphAssetGuids.Clear();

            while (wavefront.Count > 0)
            {
                var subGraphData = wavefront.Pop();
                if (!subGraphDepths.TryGetValue(subGraphData.assetGuid, out var depth))
                {
                    depth = 0;
                    subGraphDatasToBeProcessed.Add(subGraphData);
                }
                subGraphDepths[subGraphData.assetGuid] = depth + 1;

                var dependentGraphs = dependencyMap[subGraphData.assetGuid];
                foreach (var dependentGraph in dependentGraphs)
                {
                    wavefront.Push(dependentGraph);
                }
            }
            
            subGraphDatasToBeProcessed.Sort((s1,s2) => subGraphDepths[s1.assetGuid] - subGraphDepths[s2.assetGuid]);

            var registry = new FunctionRegistry(new ShaderStringBuilder(), true);

            var nestedSubGraphGuids = new HashSet<string>();
            foreach (var subGraphData in subGraphDatasToBeProcessed)
            {
                try
                {
                    if (!graphMap.TryGetValue(subGraphData.assetGuid, out var graphData))
                    {
                        var subGraphPath = AssetDatabase.GUIDToAssetPath(subGraphData.assetGuid);
                        var textGraph = File.ReadAllText(subGraphPath, Encoding.UTF8);
                        graphData = new GraphData { isSubGraph = true };
                        JsonUtility.FromJsonOverwrite(textGraph, graphData);
                    }
                    ProcessSubGraph(subGraphMap, registry, subGraphData, graphData, nestedSubGraphGuids);
                    subGraphData.isValid = true;
                }
                catch (Exception e)
                {
                    subGraphData.isValid = false;
                    var subGraphAsset = AssetDatabase.LoadAssetAtPath<SubGraphAsset>(AssetDatabase.GUIDToAssetPath(subGraphData.assetGuid));
                    Debug.LogException(e, subGraphAsset);
                }
                finally
                {
                    subGraphData.processedAt = currentTime;
                    nestedSubGraphGuids.Clear();
                }
            }
            
            // Carry over functions used by sub-graphs that were not re-processed in this import.
            foreach (var subGraphData in database.subGraphs)
            {
                foreach (var functionName in subGraphData.functionNames)
                {
                    if (!registry.sources.ContainsKey(functionName))
                    {
                        registry.sources.Add(functionName, database.functionSources[database.functionNames.BinarySearch(functionName)]);
                    }
                }
            }

            var functions = registry.sources.ToList();
            functions.Sort((p1, p2) => p1.Key.CompareTo(p2.Key));
            database.functionNames.Clear();
            database.functionSources.Clear();
            foreach (var pair in functions)
            {
                database.functionNames.Add(pair.Key);
                database.functionSources.Add(pair.Value);
            }

            ctx.AddObjectToAsset("MainAsset", database);
            ctx.SetMainObject(database);

            SubGraphDatabase.instance = null;
        }

        static void ProcessSubGraph(Dictionary<string, SubGraphData> subGraphMap, FunctionRegistry registry, SubGraphData subGraphData, GraphData graph, HashSet<string> nestedSubGraphGuids)
        {
            registry.names.Clear();
            subGraphData.functionNames.Clear();
            subGraphData.properties.Clear();
            
            graph.OnEnable();
            graph.ValidateGraph();

            var assetPath = AssetDatabase.GUIDToAssetPath(subGraphData.assetGuid);
            subGraphData.hlslName = NodeUtils.GetHLSLSafeName(Path.GetFileNameWithoutExtension(assetPath));
            subGraphData.inputStructName = $"Bindings_{subGraphData.hlslName}_{subGraphData.assetGuid}";
            subGraphData.functionName = $"{subGraphData.hlslName}_{subGraphData.assetGuid}";
            subGraphData.path = graph.path;

            var outputNode = (SubGraphOutputNode)graph.outputNode;
            
            List<AbstractMaterialNode> nodes = new List<AbstractMaterialNode>();
            NodeUtils.DepthFirstCollectNodesFromNode(nodes, outputNode);

            foreach (var node in nodes)
            {
                if (node.hasError)
                {
                    throw new InvalidOperationException("Sub-graph contains node(s) with error.");
                }
            }

            foreach (var subGraphGuid in subGraphData.subGraphGuids)
            {
                nestedSubGraphGuids.Add(subGraphGuid);
            }
            
            foreach (var node in nodes)
            {
                if (node is SubGraphNode subGraphNode)
                {
                    var nestedData = subGraphMap[subGraphNode.subGraphGuid];
                    if (!nestedData.isValid)
                    {
                        throw new InvalidOperationException("A nested sub-graph is invalid.");
                    }

                    foreach (var nestedSubGraphGuid in nestedData.subGraphGuids)
                    {
                        if (nestedSubGraphGuids.Add(nestedSubGraphGuid))
                        {
                            subGraphData.subGraphGuids.Add(nestedSubGraphGuid);
                        }
                    }
                    
                    foreach (var functionName in nestedData.functionNames)
                    {
                        registry.names.Add(functionName);
                    }
                }
                else if (node is IGeneratesFunction generatesFunction)
                {
                    generatesFunction.GenerateNodeFunction(registry, new GraphContext(subGraphData.inputStructName), GenerationMode.ForReals);
                }
            }
            
            subGraphData.outputs.Clear();
            outputNode.GetInputSlots(subGraphData.outputs);

            subGraphData.effectiveShaderStage = ShaderStageCapability.All;
            foreach (var slot in subGraphData.outputs)
            {
                var stage = NodeUtils.GetEffectiveShaderStageCapability(slot, true);
                if (stage != ShaderStageCapability.All)
                {
                    subGraphData.effectiveShaderStage = stage;
                    break;
                }
            }

            subGraphData.requirements = ShaderGraphRequirements.FromNodes(nodes, subGraphData.effectiveShaderStage, false);
            subGraphData.inputs = graph.properties.ToList();

            registry.ProvideFunction(subGraphData.functionName, sb =>
            {
                var graphContext = new GraphContext(subGraphData.inputStructName);

                GraphUtil.GenerateSurfaceInputStruct(sb, subGraphData.requirements, subGraphData.inputStructName);
                sb.AppendNewLine();

                // Generate arguments... first INPUTS
                var arguments = new List<string>();
                foreach (var prop in subGraphData.inputs)
                    arguments.Add(string.Format("{0}", prop.GetPropertyAsArgumentString()));

                // now pass surface inputs
                arguments.Add(string.Format("{0} IN", subGraphData.inputStructName));

                // Now generate outputs
                foreach (var output in subGraphData.outputs)
                    arguments.Add($"out {output.concreteValueType.ToString(outputNode.precision)} {output.shaderOutputName}");

                // Create the function prototype from the arguments
                sb.AppendLine("void {0}({1})"
                    , subGraphData.functionName
                    , arguments.Aggregate((current, next) => $"{current}, {next}"));

                // now generate the function
                using (sb.BlockScope())
                {
                    // Just grab the body from the active nodes
                    var bodyGenerator = new ShaderGenerator();
                    foreach (var node in nodes)
                    {
                        if (node is IGeneratesBodyCode)
                            (node as IGeneratesBodyCode).GenerateNodeCode(bodyGenerator, graphContext, GenerationMode.ForReals);
                    }

                    foreach (var slot in subGraphData.outputs)
                        bodyGenerator.AddShaderChunk($"{slot.shaderOutputName} = {outputNode.GetSlotValue(slot.id, GenerationMode.ForReals)};");

                    sb.Append(bodyGenerator.GetShaderString(1));
                }
            });
            
            subGraphData.functionNames.AddRange(registry.names.Distinct());

            var collector = new PropertyCollector();
            subGraphData.properties = collector.properties;
            foreach (var node in nodes)
            {
                node.CollectShaderProperties(collector, GenerationMode.ForReals);
            }
            
            subGraphData.OnBeforeSerialize();
        }
    }
}
