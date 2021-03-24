using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Needle.SelectiveProfiling;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

public class BasicTests
{
    // // A Test behaves as an ordinary method
    // [Test]
    // public void BasicTestsSimplePasses()
    // {
    // }

    [SetUp]
    public void SetUp()
    {
        Profiler.enabled = true;
    }

    [TearDown]
    public void TearDown()
    {
        Profiler.enabled = false;
    }

    // manual way (should work for editor tests)
    // we're turning on the Profiler, and use the callback to get the profiled frames.
    // we can analyze these to check if injected sampling worked.
    [UnityTest]
    public IEnumerator PatchBasicBehaviour()
    {
        var go = new GameObject("Test");
        var behaviour = go.AddComponent<BasicBehaviour>();

        int receivedProfilerFrames = 0;
        
        // hook into profiler
        ProfilerDriver.NewProfilerFrameRecorded += (connectionId, newFrameIndex) =>
        {
            receivedProfilerFrames++;
            Debug.Log("Got new profiler frame");
        };
        
        // patch
        var methodInfo = typeof(BasicBehaviour).GetMethod(nameof(BasicBehaviour.MyCall), (BindingFlags) (-1));
        SelectiveProfiler.EnableProfilingAsync(methodInfo, false, true, true, false).GetAwaiter().GetResult();
        
        Profiler.enabled = true;
        
        // call method
        behaviour.MyCall(100000);

        while (receivedProfilerFrames < 10)
            yield return null;

        Debug.Log("Received some profiler frames");

        Profiler.enabled = false;
        
        GameObject.Destroy(go);
        
        yield return null;
    }
}
