using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Needle.SelectiveProfiling;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;

public class PerformanceTests
{
    
    [Performance, UnityTest]
    public IEnumerator PatchingPerformance()
    {
        var go = new GameObject("Test");
        var behaviour = go.AddComponent<BasicBehaviour>();
        var methodInfo = typeof(BasicBehaviour).GetMethod(nameof(BasicBehaviour.MyCall), (BindingFlags) (-1));
        TestHelpers.MustNotBePatched(methodInfo);
        
        var unpatchedGroup = new SampleGroup("Unpatched Method", SampleUnit.Microsecond);
        var patchedGroup = new SampleGroup("Patched Method", SampleUnit.Microsecond);

        int warmupCount = 20;
        int measurementCount = 200;
        int iterationsPerMeasurement = 5;
        Action action = () => {
            behaviour.MyCall(10);
        }; 
        
        // regular method, not patched
        Measure
            .Method(action)
            .WarmupCount(warmupCount)
            .MeasurementCount(measurementCount)
            .IterationsPerMeasurement(iterationsPerMeasurement)
            .GC()
            .SampleGroup(unpatchedGroup)
            .Run();
        
        using(Measure.Scope("Enable Selective Profiling for " + methodInfo))
        {
            var task1 = SelectiveProfiler.EnableProfilingAsync(methodInfo, false, true);
            while (!task1.IsCompleted)
                yield return null;
        }
        
        // patched method
        Measure
            .Method(action)
            .WarmupCount(warmupCount)
            .MeasurementCount(measurementCount)
            .IterationsPerMeasurement(iterationsPerMeasurement)
            .GC()
            .SampleGroup(patchedGroup)
            .Run();
        
        using (Measure.Scope("Disable Selective Profiling for " + methodInfo))
        {

            var task = SelectiveProfiler.DisableAndForget(methodInfo);
            while (!task.IsCompleted)
                yield return null;
        }

        TestHelpers.MustNotBePatched(methodInfo); 
    }

    
    [Explicit]
    [Performance]
    [UnityTest]
    public IEnumerator Example_CubeInstantiationPerformance()
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

}
