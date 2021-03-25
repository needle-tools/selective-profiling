using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Needle.SelectiveProfiling;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

public class TestsUsingProfilerWindow
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
    public IEnumerator InjectAndCollectSamplesFromProfilerWindow()
    {
        var go = new GameObject("Test");
        var behaviour = go.AddComponent<BasicBehaviour>();

        int receivedProfilerFrames = 0;
                
        // patch
        var methodInfo = typeof(BasicBehaviour).GetMethod(nameof(BasicBehaviour.MyCall), (BindingFlags) (-1));

        TestsUsingPerformanceAPI.MustNotBePatched(methodInfo);
        
        var patcher = new TestsUsingPerformanceAPI.PatchMethod(methodInfo, true);
        yield return patcher;
        var expectedSamples = patcher.InjectedSampleNames;
        var expectedSampleCount = expectedSamples.Count();
        var receivedSamples = new List<string>();

        int collectMaxProfilerFrames = 100;
        int collectedUniqueSamples = 0;
        
        // hook into profiler
        Action<int, int> action = (int a, int b) => { };
        action = (connectionId, newFrameIndex) =>
        {
            receivedProfilerFrames++;
            
            var rawFrame = ProfilerDriver.GetRawFrameDataView(newFrameIndex, 0);
            if (!rawFrame.valid) return;

            int expectedSamplesInThisFrame = 0;
            
            for (int i = 0; i < rawFrame.sampleCount; i++)
            {
                var sampleName = rawFrame.GetSampleName(i);
                if (expectedSamples.Contains(sampleName) && !receivedSamples.Contains(sampleName))
                {
                    expectedSamplesInThisFrame++;
                    collectedUniqueSamples++;
                    receivedSamples.Add(sampleName);
                }
            }

            if(expectedSamplesInThisFrame > 0)
                Debug.Log("Profiler frame " + receivedProfilerFrames + ": got " + expectedSamplesInThisFrame + " of the expected samples");
            
            if(receivedProfilerFrames > collectMaxProfilerFrames || collectedUniqueSamples == expectedSampleCount) {
                Profiler.enabled = false;
                ProfilerDriver.NewProfilerFrameRecorded -= action;
            }
        };
        ProfilerDriver.NewProfilerFrameRecorded += action;
        Profiler.enabled = true;
        
        // ensure profiler window is open // TODO does this work on headless?
        var ProfilerWindow = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.ProfilerWindow", false);
        EditorWindow.GetWindow(ProfilerWindow).Show();

        int maxTotalFrames = 200;
        
        while (receivedProfilerFrames < collectMaxProfilerFrames && maxTotalFrames > 0)
        {
            // call method
            behaviour.MyCall(20);
            maxTotalFrames--;
            yield return null;
        }
        
        Assert.Greater(receivedProfilerFrames, 0, "Received not enough profiler frames from the ProfilerDriver. Is the Profiler window open?");
        var log = $"\n[Expected: {expectedSamples.Count} samples]\n{string.Join("\n", expectedSamples)}\n\n" +
                  $"[Actual: {receivedSamples.Count} samples]\n{string.Join("\n", receivedSamples)}\n";
        CollectionAssert.AreEqual(expectedSamples, receivedSamples, log);
        
        Debug.Log(log);

        var task = SelectiveProfiler.DisableAndForget(methodInfo);
        while (!task.IsCompleted)
            yield return null;

        Profiler.enabled = false;
        
        TestsUsingPerformanceAPI.MustNotBePatched(methodInfo);
    }
}
