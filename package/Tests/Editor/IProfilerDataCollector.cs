using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IProfilerDataCollector
{
    List<string> ReceivedSamples { get; }
    IEnumerator CollectSamples();
}
