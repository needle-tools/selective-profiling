using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Needle.SelectiveProfiling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;

public static class TestHelpers
{
    internal class Sampler
    {
        public string Name { get; }

        public bool HasCollectedDataAfterStopping
        {
            get
            {
                recorder.enabled = false;
                return recorder.elapsedNanoseconds > 0;
            }
        }
        
        private Recorder recorder;

        public Sampler(string name)
        {
            recorder = Recorder.Get(name);
            recorder.enabled = true;
            Name = name;
        }

        public override string ToString() => $"{Name} - ns: {recorder.elapsedNanoseconds}";
    }

    internal static T CreateObjectWithComponent<T>() where T:Component
    {
        var go = new GameObject("Test");
        var behaviour = go.AddComponent<T>();
        return behaviour;
    }

    internal static MethodInfo GetMethodInfo(Type type, string method)
    {
        return type.GetMethods((BindingFlags)(-1)).FirstOrDefault(x => x.Name.Equals(method, StringComparison.Ordinal));
    }

    internal static void MustNotBePatched(MethodInfo methodInfo)
    {
        Assert.IsFalse(SelectiveProfiler.TryGet(methodInfo, out var info), "Method is patched: " + methodInfo);
    }
    
    internal class PatchMethod : CustomYieldInstruction
    {
        private readonly bool shouldCollectNames = false;
        private readonly Task patchingTask;
        
        public List<string> InjectedSampleNames => shouldCollectNames ? injectedSamples : throw new System.InvalidOperationException("PatchMethod has been called without sample request, this is not allowed");
        private volatile List<string> injectedSamples = new List<string>();
        
        public PatchMethod(MethodInfo methodInfo, bool collectInjectedSampleNames = false)
        {
            shouldCollectNames = collectInjectedSampleNames;
            
            if(collectInjectedSampleNames)
                ProfilerSamplePatch.OnSampleInjected += (methodBase, sampleName) =>
                {
                    if(!injectedSamples.Contains(sampleName))
                        injectedSamples.Add(sampleName);
                };
            
            patchingTask = SelectiveProfiler.EnableProfilingAsync(methodInfo, false, true, true, false);
        }

        public override bool keepWaiting
        {
            get
            {
                if (patchingTask.IsCompleted) {
                    if (shouldCollectNames)
                        ProfilerSamplePatch.OnSampleInjected = null;
                    return false;
                }

                return true;
            }
        }
    }

    internal static bool MethodIsPatched(Action callMethod, List<string> injectedSampleNames)
    {
        var recorders = injectedSampleNames.Select(x => new Sampler(x)).ToList();
        callMethod();
        return recorders.Any(x => x.HasCollectedDataAfterStopping);
    }

}