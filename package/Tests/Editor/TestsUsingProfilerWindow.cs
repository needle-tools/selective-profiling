using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Needle.SelectiveProfiling;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

public class TestsUsingProfilerWindow
{
    // manual way (should work for editor tests)
    // we're turning on the Profiler, and use the callback to get the profiled frames.
    // we can analyze these to check if injected sampling worked.
    [UnityTest]
    public IEnumerator InjectAndCollectSamplesFromProfilerWindow()
    {
        var go = new GameObject("Test");
        var behaviour = go.AddComponent<BasicBehaviour>();
                
        // patch
        var methodInfo = typeof(BasicBehaviour).GetMethod(nameof(BasicBehaviour.MyCall), (BindingFlags) (-1));

        TestHelpers.MustNotBePatched(methodInfo);
        
        var patcher = new TestHelpers.PatchMethod(methodInfo, true);
        yield return patcher;
        var expectedSamples = patcher.InjectedSampleNames;

        var collector = new ProfilerSampleCollector(methodInfo, expectedSamples, () => behaviour.MyCall(100));
        yield return collector;

        var receivedSamples = collector.ReceivedSamples;

        var task = SelectiveProfiler.DisableAndForget(methodInfo);
        while (!task.IsCompleted)
            yield return null;

        Profiler.enabled = false;

        var log = TestHelpers.Log(expectedSamples, receivedSamples);
        CollectionAssert.AreEqual(expectedSamples, receivedSamples, log);
        
        Debug.Log(log);

        TestHelpers.MustNotBePatched(methodInfo); 
    }
    
    
    [UnityTest]
    public IEnumerator ProfilerSamplesAreCollected()
    {
        var behaviour = TestHelpers.CreateObjectWithComponent<BasicBehaviour>();
        void Action() => behaviour.MyCall(10000);
        var methodInfo = TestHelpers.GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MyCall));
        
        TestHelpers.MustNotBePatched(methodInfo);
        
        var patching = new TestHelpers.PatchMethod(methodInfo, true);
        yield return patching;
        
        var collectorThatShouldReceiveSamples = new ProfilerSampleCollector(methodInfo, patching.InjectedSampleNames, Action);
        yield return collectorThatShouldReceiveSamples;
        
        var task = SelectiveProfiler.DisableAndForget(methodInfo);
        while (!task.IsCompleted)
            yield return null;
        
        CollectionAssert.AreEqual(patching.InjectedSampleNames, collectorThatShouldReceiveSamples.ReceivedSamples, 
            TestHelpers.Log(patching.InjectedSampleNames, collectorThatShouldReceiveSamples.ReceivedSamples));
                
        TestHelpers.MustNotBePatched(methodInfo);
    }

    [UnityTest]
    public IEnumerator NewCollectorApproach() 
    {
        var behaviour = TestHelpers.CreateObjectWithComponent<BasicBehaviour>();
        void Action() => behaviour.MyCall(10000);
        var methodInfo = TestHelpers.GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MyCall));
        
        TestHelpers.MustNotBePatched(methodInfo);
        
        var patching = new TestHelpers.PatchMethod(methodInfo, true);
        yield return patching;
        
        var collector = new ProfilerSampleCollector(methodInfo, patching.InjectedSampleNames, Action);
        yield return collector.Collect();
        Debug.Log("Done");
    }
}
