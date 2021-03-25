using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Needle.SelectiveProfiling;
using NUnit.Framework;
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
        
        var log = 
            $"\n[Expected: {expectedSamples.Count} samples]\n{string.Join("\n", expectedSamples)}\n\n" +
            $"[Actual: {receivedSamples.Count} samples]\n{string.Join("\n", receivedSamples)}\n";
        CollectionAssert.AreEqual(expectedSamples, receivedSamples, log);
        
        Debug.Log(log);

        
        TestHelpers.MustNotBePatched(methodInfo);
    }
}
