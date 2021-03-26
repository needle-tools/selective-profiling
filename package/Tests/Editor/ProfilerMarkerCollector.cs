using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class ProfilerMarkerCollector : IProfilerDataCollector
{
    private Action action;
    private List<string> expectedSampleNames;
    public List<string> ReceivedSamples { get; private set; } = new List<string>();
    
    public ProfilerMarkerCollector(List<string> expectedSampleNames, Action action)
    {
        this.expectedSampleNames = expectedSampleNames;
        this.action = action;
    }

    public IEnumerator CollectSamples()
    {
        var recorders = expectedSampleNames.Select(x => new TestHelpers.Sampler(x)).ToList();
        action();
        ReceivedSamples = recorders.Where(x => x.HasCollectedDataAfterStopping).Select(x => x.Name).ToList();
        
        yield break;
    }
}
