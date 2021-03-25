using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IProfilerCollector
{
    List<string> ReceivedSamples { get; }
}
