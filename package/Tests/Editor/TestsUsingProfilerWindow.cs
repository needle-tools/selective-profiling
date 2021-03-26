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
    [UnityTest]
    public IEnumerator ProfilerSamplesAreCollected() 
    {
        var behaviour = TestHelpers.CreateObjectWithComponent<BasicBehaviour>();
        void Action() => behaviour.MyCall(1000);
        var methodInfo = TestHelpers.GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MyCall));
        
        TestHelpers.AssertIsNotPatched(methodInfo);
        
        var patching = new TestHelpers.PatchMethod(methodInfo, true);
        yield return patching;
        
        TestHelpers.AssertIsPatched(methodInfo);
        
        var collector = new ProfilerSampleCollector(patching.InjectedSampleNames, Action);
        yield return collector.CollectSamples();
        
        var task = SelectiveProfiler.DisableAndForget(methodInfo);
        while (!task.IsCompleted)
            yield return null;

        TestHelpers.AssertSamplesAreEqual(patching.InjectedSampleNames, collector.ReceivedSamples);
        TestHelpers.AssertIsNotPatched(methodInfo);
    }
}
