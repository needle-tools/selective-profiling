using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class ProfilerMarkerCollector : CustomYieldInstruction, IProfilerDataCollector
{
    public List<string> ReceivedSamples { get; private set; } = new List<string>();
    
    public ProfilerMarkerCollector(MethodInfo methodInfo, List<string> expectedSampleNames, Action action)
    {
        var recorders = expectedSampleNames.Select(x => new TestHelpers.Sampler(x)).ToList();
        action();
        ReceivedSamples = recorders.Where(x => x.HasCollectedDataAfterStopping).Select(x => x.Name).ToList();
    }

    public override bool keepWaiting => false;
}
