using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Needle.SelectiveProfiling;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

internal class ProfilerSampleCollector : CustomYieldInstruction, IProfilerDataCollector
{
    public IEnumerator Collect()
    {
        ProfilerDriver.profileEditor = true;
        ProfilerDriver.enabled = true;
        yield return null;

        action();
        
        ProfilerDriver.enabled = false;
        ProfilerDriver.profileEditor = false;
        yield return null;
        
        Assert.GreaterOrEqual(ProfilerDriver.lastFrameIndex, 0);

        // using (var frameData = ProfilerDriver.GetHierarchyFrameDataView(ProfilerDriver.lastFrameIndex, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnDontSort, false))
        // {
        //     var sampleId = frameData.FindChildItemByFunctionNameRecursively(0, "Test Marker A");
        //     Assert.AreNotEqual(HierarchyFrameDataView.invalidSampleId, sampleId);
        // }

        using (var rawFrame = ProfilerDriver.GetRawFrameDataView(ProfilerDriver.lastFrameIndex, 0))
        {
            Assert.IsTrue(rawFrame.valid);

            int expectedSamplesInThisFrame = 0;
            
            for (int i = 0; i < rawFrame.sampleCount; i++)
            {
                var sampleName = rawFrame.GetSampleName(i);
                if (expectedSamples.Contains(sampleName) && !ReceivedSamples.Contains(sampleName))
                {
                    expectedSamplesInThisFrame++;
                    ReceivedSamples.Add(sampleName);
                }
            }
        }
    }
    
    public List<string> ReceivedSamples { get; private set; } = new List<string>();
    
    private int maxTotalFrames;
    private int collectMaxProfilerFrames;
    
    private MethodInfo methodInfo;
    private readonly List<string> expectedSamples;
    private readonly Action action;
    private int receivedProfilerFrames = 0;
    private bool haveCollectedAllSamples = false;
    
    public ProfilerSampleCollector(MethodInfo methodInfo, List<string> expectedSampleNames, Action action)
    {
        this.methodInfo = methodInfo;
        this.expectedSamples = expectedSampleNames;
        this.action = action;
        maxTotalFrames = 200;
        collectMaxProfilerFrames = 100;            
        Setup();
    }

    private void Setup()
    {
        var expectedSampleCount = expectedSamples.Count();
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
                if (expectedSamples.Contains(sampleName) && !ReceivedSamples.Contains(sampleName))
                {
                    expectedSamplesInThisFrame++;
                    collectedUniqueSamples++;
                    ReceivedSamples.Add(sampleName);
                }
            }

            if(expectedSamplesInThisFrame > 0)
                Debug.Log("Profiler frame " + receivedProfilerFrames + ": got " + expectedSamplesInThisFrame + " of the expected samples");
            
            if(receivedProfilerFrames > collectMaxProfilerFrames || collectedUniqueSamples == expectedSampleCount)
            {
                haveCollectedAllSamples = true;
                ProfilerDriver.enabled = false;
                ProfilerDriver.NewProfilerFrameRecorded -= action;
            }
        };
        ProfilerDriver.NewProfilerFrameRecorded += action;

        ProfilerDriver.profileEditor = true;
        ProfilerDriver.enabled = true;
        
        // // ensure profiler window is open // TODO does this work on headless?
        // var ProfilerWindow = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProfilerWindow", false);
        // EditorWindow.GetWindow(ProfilerWindow).Show();
    }

    private void Cleanup()
    {
        // 
    }

    public override bool keepWaiting
    {
        get
        {
            action();
            maxTotalFrames--;

            bool doKeepWaiting = receivedProfilerFrames < collectMaxProfilerFrames && maxTotalFrames > 0 && !haveCollectedAllSamples;
            if (!doKeepWaiting)
            {
                Assert.Greater(receivedProfilerFrames, 0, "Received not enough profiler frames from the ProfilerDriver. Is the Profiler window open?");
                Cleanup();
            }

            // Debug.Log($"Frames so far: {maxTotalFrames}, received profiler frames: {receivedProfilerFrames} - keep waiting: {doKeepWaiting}");
            
            return doKeepWaiting;
        }
    }
}
    