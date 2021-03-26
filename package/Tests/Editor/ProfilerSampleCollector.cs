using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditorInternal;

internal class ProfilerSampleCollector : IProfilerDataCollector
{    
    public List<string> ReceivedSamples { get; private set; } = new List<string>();
    private readonly List<string> expectedSamples;
    private readonly Action action;
    
    public ProfilerSampleCollector(List<string> expectedSampleNames, Action action)
    {
        this.expectedSamples = expectedSampleNames;
        this.action = action;
    }
    
    public IEnumerator CollectSamples()
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
}
    