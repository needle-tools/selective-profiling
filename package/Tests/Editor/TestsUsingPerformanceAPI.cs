using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
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

    class Sampler
    {
        public string Name { get; }

        public bool HasCollectedDataAfterStopping
        {
            get
            {
                recorder.enabled = false;
                return recorder.elapsedNanoseconds > 0;
            }
        }
        
        private Recorder recorder;

        public Sampler(string name)
        {
            recorder = Recorder.Get(name);
            recorder.enabled = true;
            Name = name;
        }

        public override string ToString() => $"{Name} - ns: {recorder.elapsedNanoseconds}";
    }

    T CreateObjectWithComponent<T>() where T:Component
    {
        var go = new GameObject("Test");
        var behaviour = go.AddComponent<T>();
        return behaviour;
    }

    static MethodInfo GetMethodInfo(Type type, string method)
    {
        return type.GetMethods((BindingFlags)(-1)).FirstOrDefault(x => x.Name.Equals(method, StringComparison.Ordinal));
    }

    class PatchMethod : CustomYieldInstruction
    {
        private readonly bool shouldCollectNames = false;
        private readonly Task patchingTask;
        
        public List<string> InjectedSampleNames => shouldCollectNames ? injectedSamples : throw new System.InvalidOperationException("PatchMethod has been called without sample request, this is not allowed");
        private volatile List<string> injectedSamples = new List<string>();
        
        public PatchMethod(MethodInfo methodInfo, bool collectInjectedSampleNames = false)
        {
            shouldCollectNames = collectInjectedSampleNames;
            
            if(collectInjectedSampleNames)
                ProfilerSamplePatch.OnSampleInjected += (methodBase, sampleName) => injectedSamples.Add(sampleName);
            
            patchingTask = SelectiveProfiler.EnableProfilingAsync(methodInfo, false, true, true, false);
        }

        public override bool keepWaiting
        {
            get
            {
                if (patchingTask.IsCompleted) {
                    if (shouldCollectNames)
                        ProfilerSamplePatch.OnSampleInjected = null;
                    return false;
                }

                return true;
            }
        }
    }

    bool MethodIsPatched(Action callMethod, List<string> injectedSampleNames)
    {
        var recorders = injectedSampleNames.Select(x => new Sampler(x)).ToList();
        callMethod();
        return recorders.Any(x => x.HasCollectedDataAfterStopping);
    }

    public class PatchingTestCase
    {
        public MethodInfo methodInfo;
        
        public PatchingTestCase(MethodInfo methodInfo)
        {
            this.methodInfo = methodInfo;
        }

        public override string ToString()
        {
            return methodInfo.DeclaringType.FullName + "." + methodInfo;
        }
    }

    static IEnumerable<PatchingTestCase> TestCasesThatShouldHaveInjectedSamples()
    {
        yield return new PatchingTestCase(GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MyStaticCall)));
        yield return new PatchingTestCase(GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MyCall)));
        yield return new PatchingTestCase(GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MethodWithCallsBeforeTry_MustSucceed)));
        yield return new PatchingTestCase(GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MethodWithCallsAfterTry_MustSucceed)));
    }
    
    static IEnumerable<PatchingTestCase> TestCasesThatShouldHaveNoInjectedSamples()
    {
        yield return new PatchingTestCase(GetMethodInfo(typeof(Profiler), nameof(Profiler.BeginSample)));
        yield return new PatchingTestCase(GetMethodInfo(typeof(Profiler), nameof(Profiler.EndSample)));
        yield return new PatchingTestCase(GetMethodInfo(typeof(ProfilerMarker), nameof(ProfilerMarker.Begin)));
        yield return new PatchingTestCase(GetMethodInfo(typeof(ProfilerMarker), nameof(ProfilerMarker.End)));
        yield return new PatchingTestCase(GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MethodWithNoCalls_MustFail)));
        yield return new PatchingTestCase(GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MethodWithCallsInsideCatch_MustFail)));
        yield return new PatchingTestCase(GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MethodWithCallsInsideTry_MustFail)));
        yield return new PatchingTestCase(GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MethodWithCallsInsideNestedTry_MustFail)));
    }
    
    [UnityTest]
    public IEnumerator PatchHasInjectedSamples([ValueSource(nameof(TestCasesThatShouldHaveInjectedSamples))] PatchingTestCase testCase)
    {
        var patchMethod = new PatchMethod(testCase.methodInfo, true);
        yield return patchMethod;
        CollectionAssert.IsNotEmpty(patchMethod.InjectedSampleNames, "No samples injected into " + testCase.methodInfo);
        
        // clean up
        var task = SelectiveProfiler.DisableAndForget(testCase.methodInfo);
        while(!task.IsCompleted)
            yield return null;
    }

    [UnityTest]
    public IEnumerator PatchHasNoInjectedSamples([ValueSource(nameof(TestCasesThatShouldHaveNoInjectedSamples))] PatchingTestCase testCase)
    {
        var patchMethod = new PatchMethod(testCase.methodInfo, true);
        yield return patchMethod;
        CollectionAssert.IsEmpty(patchMethod.InjectedSampleNames, "Samples have been injected into " + testCase.methodInfo);
        
        // clean up
        var task = SelectiveProfiler.DisableAndForget(testCase.methodInfo);
        while(!task.IsCompleted)
            yield return null;
    }

    public class NamespaceTestCase
    {
        public string Namespace { get; }
        public IEnumerable<Type> Types { get; }
        
        public NamespaceTestCase(string nameSpace, IEnumerable<Type> namespaceToType)
        {
            this.Namespace = nameSpace;
            this.Types = namespaceToType;
        }

        public override string ToString()
        {
            return Namespace + " (" + Types.Count() + " types)";
        }
    }

    static IEnumerable<NamespaceTestCase> TestCasesPerNamespace()
    {
        // get all types
        var namespaceToTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetLoadableTypes()).ToLookup(x => x.Namespace);
        foreach (var group in namespaceToTypes)
        {
            yield return new NamespaceTestCase(group.Key, namespaceToTypes[group.Key]);
        }
    }

    [Ignore("Very slow, please run manually")]
    [UnityTest]
    public IEnumerator CanPatchEverythingInNamespace([ValueSource(nameof(TestCasesPerNamespace))] NamespaceTestCase testCase)
    {
        var logBefore = SelectiveProfilerSettings.instance.DebugLog; 
        SelectiveProfilerSettings.instance.DebugLog = false;
        
        // for each type, try to patch all methods
        var methods = testCase.Types.SelectMany(x => x.GetMethods((BindingFlags) (-1)));
        var methodsThatDidntHaveSamples = new List<MethodInfo>();
        
        foreach (var methodInfo in methods)
        {
            var patchMethod = new PatchMethod(methodInfo, true);
            yield return patchMethod;

            if (!patchMethod.InjectedSampleNames.Any())
                methodsThatDidntHaveSamples.Add(methodInfo);
            
            var task = SelectiveProfiler.DisableAndForget(methodInfo);
            while (!task.IsCompleted)
                yield return null;
        }

        var allMethodsCount = methods.Count();
        var methodsThatDidntHaveSamplesCount = methodsThatDidntHaveSamples.Count();
        var percentageWithSamples = (float) methodsThatDidntHaveSamplesCount / allMethodsCount * 100f;

        var log = $"Methods without samples ({methodsThatDidntHaveSamplesCount}/{allMethodsCount}, {percentageWithSamples:F2}%): \n{string.Join("\n", methodsThatDidntHaveSamples)}";
        Debug.Log(log);
        
        // We know we can't patch everything here - this might be more suited for another test that only patches methods that we think should succeed
        // CollectionAssert.IsEmpty(methodsThatDidntHaveSamples, log);
        
        SelectiveProfilerSettings.instance.DebugLog = logBefore;
    }

    [UnityTest]
    public IEnumerator MinimalExample()
    {
        void Action() => BasicBehaviour.MyStaticCall();
        var methodInfo = GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MyStaticCall));
        
        var patchMethod = new PatchMethod(methodInfo, true);
        yield return patchMethod;
        
        Assert.IsTrue(MethodIsPatched(Action, patchMethod.InjectedSampleNames), 
            $"MethodIsPatched(Action, patchMethod.InjectedSampleNames)\n" +
            $"[Expected Sample Names: {patchMethod.InjectedSampleNames.Count}]\n{string.Join("\n", patchMethod.InjectedSampleNames)}");
        
        var task = SelectiveProfiler.DisableAndForget(methodInfo);
        while (!task.IsCompleted)
            yield return null;
        
        Assert.IsFalse(MethodIsPatched(Action, patchMethod.InjectedSampleNames), 
            $"MethodIsPatched(Action, patchMethod.InjectedSampleNames)\n" +
            $"[Must not have Samples: {patchMethod.InjectedSampleNames.Count}]\n{string.Join("\n", patchMethod.InjectedSampleNames)}");
    }

    [UnityTest]
    public IEnumerator PatchingAndUnpatching_MethodIsGone()
    {
        var methodInfo = GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MyCall));
        yield return new PatchMethod(methodInfo);
        
        if (SelectiveProfiler.TryGet(methodInfo, out var profilingInfo))
        {
            Assert.NotNull(profilingInfo.Patch, "profilingInfo.Patch != null");
            Assert.IsTrue(profilingInfo.IsActive, "profilingInfo.IsActive");
        }
        else
        {
            Assert.Fail("Method is not patched");
        }

        // TODO this pretends to be sync but is async, how can we actually check the method isn't patched anymore?
        SelectiveProfiler.DisableAndForget(methodInfo);

        if (SelectiveProfiler.TryGet(methodInfo, out var profilingInfoAfterDisable))
        {
            Assert.Fail("Method is still patched, disable didn't work");
        }
    }

    void MustNotBePatched(MethodInfo methodInfo)
    {
        Assert.IsFalse(SelectiveProfiler.TryGet(methodInfo, out var info), "Method is patched: " + methodInfo);
    }
    
    [UnityTest]
    public IEnumerator CheckIfSampleExists()
    {
        var behaviour = CreateObjectWithComponent<BasicBehaviour>();
        var methodInfo = GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MyCall));
        MustNotBePatched(methodInfo);
        
        var patching = new PatchMethod(methodInfo, true);
        yield return patching;
        injectedSamples = patching.InjectedSampleNames;
        
        // Debug.Log("Injected samples:\n" + string.Join("\n", injectedSamples));

        injectedSamples.Add("MyTest");
        string[] markers = injectedSamples.ToArray();

        int k = 0;
        
        // Profiler markers created using Profiler.BeginSample() are not supported, switch to ProfilerMarker if possible.
        var recorders = injectedSamples.Select(x => new Sampler(x)).ToList();
        
        // for testing whether we can collect stuff here
        var marker_Test = new ProfilerMarker("MyTest");

        // TODO remove unnecessary loops here, we only want to know if we can collect samples
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
        
        var recordersWithData = recorders.Where(x => x.HasCollectedDataAfterStopping).ToList();

        // prevent compiler stripping - this should always be false
        if(k < 0) Debug.Log(k);
        
        SelectiveProfiler.DisableAndForget(methodInfo);
        
        CollectionAssert.AreEqual(recorders, recordersWithData, 
            $"\n[Expected: {recorders.Count} samples]\n{string.Join("\n", recorders)}\n\n" + 
            $"[Actual: {recordersWithData.Count} samples]\n{string.Join("\n", recordersWithData)}\n");
    }

    [Performance, UnityTest]
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
    
    [Performance, Test]
    public void PatchingPerformance()
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
                // TODO wait for completion
                SelectiveProfiler.EnableProfilingAsync(methodInfo, false, true);
            })
            .CleanUp(() =>
            {
                // TODO wait for completion
                SelectiveProfiler.DisableProfiling(methodInfo, false);
            })
            .Run();
    }
    
}
