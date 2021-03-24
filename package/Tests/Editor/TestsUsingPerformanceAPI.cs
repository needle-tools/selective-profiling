using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Needle.SelectiveProfiling;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

public class TestsUsingPerformanceAPI
{
    volatile List<string> injectedSamples = new List<string>();
    
    [Performance, UnityTest]
    public IEnumerator CheckIfSampleExists()
    {
        var go = new GameObject("Test");
        var behaviour = go.AddComponent<BasicBehaviour>();
        
        // patch
        var methodInfo = typeof(BasicBehaviour).GetMethod(nameof(BasicBehaviour.MyCall), (BindingFlags) (-1));
        
        ProfilerSamplePatch.OnSampleInjected += (methodBase, sampleName) =>
        {
            injectedSamples.Add(sampleName);
        };
        
        var task = SelectiveProfiler.EnableProfilingAsync(methodInfo, false, true, true, false);
        while (!task.IsCompleted)
            yield return null;

        Debug.Log("Injected samples:\n" + string.Join("\n", injectedSamples));

        ProfilerSamplePatch.OnSampleInjected = null;

        injectedSamples.Add("Test");
        string[] markers = injectedSamples.ToArray();

        yield return null;
        yield return null;
        yield return null;
        yield return null;
        yield return null;

        int k = 0;
        
        // Profiler markers created using Profiler.BeginSample() are not supported, switch to ProfilerMarker if possible.

        var marker_Test = new ProfilerMarker("Test");
        
        using (Measure.ProfilerMarkers(markers))
        {
            for (var i = 0; i < 50; i++)
            {
                behaviour.MyCall(10000);
                
                marker_Test.Begin();
                for (int j = 0; j < 1000; j++)
                {
                    k += j;
                }
                marker_Test.End();
            }
        }

        Debug.Log(k);
        
        SelectiveProfiler.DisableAndForget(methodInfo);
    }

    [Performance, UnityTest]
    public IEnumerator PerformanceTestBasic()
    {
        string[] markers =
        {
            "Instantiate",
            "Instantiate.Copy",
            "Instantiate.Produce",
            "Instantiate.Awake"
        };

        yield return null;
        
        using (Measure.ProfilerMarkers(markers))
        {
            using(Measure.Scope())
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                for (var i = 0; i < 5000; i++)
                {
                    UnityEngine.Object.Instantiate(cube);
                }
            }
        }
    }
    
    [Test, Performance]
    public void TestPatchingPerformance()
    {
        var go = new GameObject("Test");
        var behaviour = go.AddComponent<BasicBehaviour>();
        var methodInfo = typeof(BasicBehaviour).GetMethod(nameof(BasicBehaviour.MyCall), (BindingFlags) (-1));

        var unpatchedGroup = new SampleGroup("Unpatched Method", SampleUnit.Microsecond);
        var patchedGroup = new SampleGroup("Patched Method", SampleUnit.Microsecond);
        
        // regular method, not patched
        Measure.Method(() =>
            {
                behaviour.MyCall(1000);
            })
            .WarmupCount(10)
            .MeasurementCount(20)
            .IterationsPerMeasurement(50)
            // .GC()
            .SampleGroup(unpatchedGroup)
            .Run();
        
        // patched method
        Measure.Method(() =>
            {
                behaviour.MyCall(1000);
            })
            .WarmupCount(10)
            .MeasurementCount(20)
            .IterationsPerMeasurement(50)
            // .GC()
            .SampleGroup(patchedGroup)
            .SetUp(() =>
            {
                SelectiveProfiler.EnableProfilingAsync(methodInfo, false, true).GetAwaiter().GetResult();
            })
            .CleanUp(() =>
            {
                SelectiveProfiler.DisableProfiling(methodInfo, false).GetAwaiter().GetResult();
            })
            .Run();
    }
    
}
