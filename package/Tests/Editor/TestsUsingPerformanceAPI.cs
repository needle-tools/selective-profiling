using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using needle.EditorPatching;
using Needle.SelectiveProfiling;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

public class TestsUsingPerformanceAPI
{
    #region Test Cases
    
    static IEnumerable<PatchingTestCase> TestCasesThatShouldHaveInjectedSamples()
    {
        yield return new PatchingTestCase(typeof(BasicBehaviour), nameof(BasicBehaviour.MyStaticCall));
        yield return new PatchingTestCase(typeof(BasicBehaviour), nameof(BasicBehaviour.MyCall));
        yield return new PatchingTestCase(typeof(BasicBehaviour), nameof(BasicBehaviour.MethodWithCallsBeforeTry_MustSucceed));
        yield return new PatchingTestCase(typeof(BasicBehaviour), nameof(BasicBehaviour.MethodWithCallsAfterTry_MustSucceed));
    }
    
    static IEnumerable<PatchingTestCase> TestCasesThatShouldHaveNoInjectedSamples()
    {
        yield return new PatchingTestCase(typeof(Profiler), nameof(Profiler.BeginSample));
        yield return new PatchingTestCase(typeof(Profiler), nameof(Profiler.EndSample));
        yield return new PatchingTestCase(typeof(ProfilerMarker), nameof(ProfilerMarker.Begin));
        yield return new PatchingTestCase(typeof(ProfilerMarker), nameof(ProfilerMarker.End));
        yield return new PatchingTestCase(typeof(BasicBehaviour), nameof(BasicBehaviour.MethodWithNoCalls_MustFail));
        yield return new PatchingTestCase(typeof(BasicBehaviour), nameof(BasicBehaviour.MethodWithCallsInsideCatch_MustFail));
        yield return new PatchingTestCase(typeof(BasicBehaviour), nameof(BasicBehaviour.MethodWithCallsInsideTry_MustFail));
        yield return new PatchingTestCase(typeof(BasicBehaviour), nameof(BasicBehaviour.MethodWithCallsInsideNestedTry_MustFail));
    }

    static IEnumerable<NamespaceTestCase> TestCasesPerNamespace()
    {
        // get all types
        var namespaceToTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(x => x.GetLoadableTypes())
            .ToLookup(x => x.Namespace);
        foreach (var group in namespaceToTypes.Where(x => !string.IsNullOrEmpty(x.Key) && x.Key.Contains("Unity")))
        {
            yield return new NamespaceTestCase(group.Key, namespaceToTypes[group.Key]);
        }
    }
    
    #endregion
    
    [UnityTest]
    public IEnumerator PatchHasInjectedSamples([ValueSource(nameof(TestCasesThatShouldHaveInjectedSamples))] PatchingTestCase testCase)
    {
        TestHelpers.MustNotBePatched(testCase.methodInfo);
        
        var patchMethod = new TestHelpers.PatchMethod(testCase.methodInfo, true);
        yield return patchMethod;
        
        // clean up
        var task = SelectiveProfiler.DisableAndForget(testCase.methodInfo);
        while(!task.IsCompleted)
            yield return null;
        
        CollectionAssert.IsNotEmpty(patchMethod.InjectedSampleNames, "No samples injected into " + testCase.methodInfo);
        TestHelpers.MustNotBePatched(testCase.methodInfo);
    }

    [UnityTest]
    public IEnumerator PatchHasNoInjectedSamples([ValueSource(nameof(TestCasesThatShouldHaveNoInjectedSamples))] PatchingTestCase testCase)
    {
        TestHelpers.MustNotBePatched(testCase.methodInfo);
        
        var patchMethod = new TestHelpers.PatchMethod(testCase.methodInfo, true);
        yield return patchMethod;
        
        // clean up
        var task = SelectiveProfiler.DisableAndForget(testCase.methodInfo);
        while(!task.IsCompleted)
            yield return null;
        
        CollectionAssert.IsEmpty(patchMethod.InjectedSampleNames, "Samples have been injected into " + testCase.methodInfo);
        
        TestHelpers.MustNotBePatched(testCase.methodInfo);
    }

    [UnityTest]
    public IEnumerator AllInjectedSamplesAreRecorded()
    {
        void Action() => BasicBehaviour.MyStaticCall();
        var methodInfo = TestHelpers.GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MyStaticCall));
        TestHelpers.MustNotBePatched(methodInfo);
        
        var patchMethod = new TestHelpers.PatchMethod(methodInfo, true);
        yield return patchMethod;

        TestHelpers.MustBePatched(methodInfo);
        CollectionAssert.IsNotEmpty(patchMethod.InjectedSampleNames, "No samples have been injected");
        
        Debug.Log("Done patching");
        
        var collectorThatShouldReceiveSamples = new ProfilerSampleCollector(methodInfo, patchMethod.InjectedSampleNames, Action);
        yield return collectorThatShouldReceiveSamples.CollectSamples();
        var methodIsPatchedAfterPatching = collectorThatShouldReceiveSamples.ReceivedSamples.Count == patchMethod.InjectedSampleNames.Count; 
        
        Debug.Log("Done collecting samples where we should receive some\n" + 
                  TestHelpers.Log(patchMethod.InjectedSampleNames, collectorThatShouldReceiveSamples.ReceivedSamples));
        
        var task = SelectiveProfiler.DisableAndForget(methodInfo);
        while (!task.IsCompleted)
            yield return null;

        Debug.Log("Done unpatching");
        
        TestHelpers.MustNotBePatched(methodInfo);
        
        Assert.IsTrue(methodIsPatchedAfterPatching, 
            $"MethodIsPatched(Action, patchMethod.InjectedSampleNames)\n" +
            TestHelpers.Log(patchMethod.InjectedSampleNames, collectorThatShouldReceiveSamples.ReceivedSamples));

        // seems we need to wait some arbitrary amount of time until Profiler doesn't serve old samples anymore
        // TODO can we detect that Profiler has actually stopped sending frames? 
        for(int i = 0; i < 60; i++)
            yield return null;
        
        var collectorThatShouldNotReceiveSamples = new ProfilerSampleCollector(methodInfo, patchMethod.InjectedSampleNames, Action);
        yield return collectorThatShouldNotReceiveSamples.CollectSamples();
        
        Debug.Log("Done collecting samples where we should receive 0\n" + 
                  TestHelpers.Log(patchMethod.InjectedSampleNames, collectorThatShouldNotReceiveSamples.ReceivedSamples));
        
        Assert.IsFalse(collectorThatShouldNotReceiveSamples.ReceivedSamples.Count > 0, 
            $"Collector should not have captured any samples but captured {collectorThatShouldNotReceiveSamples.ReceivedSamples.Count}\n" +
            TestHelpers.Log(patchMethod.InjectedSampleNames, collectorThatShouldNotReceiveSamples.ReceivedSamples));
    }

    [UnityTest]
    public IEnumerator PatchingAndUnpatching_MethodIsGone()
    {
        var methodInfo = TestHelpers.GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MyCall));
        TestHelpers.MustNotBePatched(methodInfo);
        
        yield return new TestHelpers.PatchMethod(methodInfo);
        
        if (SelectiveProfiler.TryGet(methodInfo, out var profilingInfo))
        {
            Assert.NotNull(profilingInfo.Patch, "profilingInfo.Patch != null");
            Assert.IsTrue(profilingInfo.IsActive, "profilingInfo.IsActive");
        }
        else
        {
            Assert.Fail("Method is not patched");
        }

        var task = SelectiveProfiler.DisableAndForget(methodInfo);
        while (!task.IsCompleted) yield return null;

        TestHelpers.MustNotBePatched(methodInfo);
    }

    [UnityTest]
    [Explicit]
    public IEnumerator ProfilerMarkersAreCollected()
    {
        var behaviour = TestHelpers.CreateObjectWithComponent<BasicBehaviour>();
        void Action() => behaviour.MyCall(10000);
        var methodInfo = TestHelpers.GetMethodInfo(typeof(BasicBehaviour), nameof(BasicBehaviour.MyCall));
        TestHelpers.MustNotBePatched(methodInfo);
        
        var patching = new TestHelpers.PatchMethod(methodInfo, true);
        yield return patching;
        var injectedSamples = new List<string>(patching.InjectedSampleNames);
        
        // Debug.Log("Injected samples:\n" + string.Join("\n", injectedSamples));

        injectedSamples.Add("MyTest");

        int k = 0;
        
        // Profiler markers created using Profiler.BeginSample() are not supported, switch to ProfilerMarker if possible.
        var recorders = injectedSamples.Select(x => new TestHelpers.Sampler(x)).ToList();
        
        // for testing whether we can collect stuff here
        var marker_Test = new ProfilerMarker("MyTest");

        // TODO remove unnecessary loops here, we only want to know if we can collect samples
        for (var i = 0; i < 50; i++)
        {
            Action();
            
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
        
        var task = SelectiveProfiler.DisableAndForget(methodInfo);
        while (!task.IsCompleted)
            yield return null;
        
        CollectionAssert.AreEqual(recorders, recordersWithData, TestHelpers.Log(recorders, recordersWithData));
        
        TestHelpers.MustNotBePatched(methodInfo);
    }

    
    // [Test]
    // public async void PatchMethodWithoutSampleRequestShouldThrow()
    // {
    //     var methodInfo = typeof(BasicBehaviour).GetMethod(nameof(BasicBehaviour.MyCall), (BindingFlags) (-1));
    //     MustNotBePatched(methodInfo);
    //
    //     Assert.Catch<System.InvalidOperationException>(async () =>
    //     {
    //         var patching = new PatchMethod(methodInfo, true);
    //         while (patching.MoveNext())
    //         {
    //         }
    //
    //         injectedSamples = patching.InjectedSampleNames;
    //     });
    // }
    
    
    [Explicit]
    [UnityTest]
    public IEnumerator CanPatchEverythingInNamespace([ValueSource(nameof(TestCasesPerNamespace))] NamespaceTestCase testCase)
    {
        var logBefore = SelectiveProfilerSettings.instance.DebugLog; 
        SelectiveProfilerSettings.instance.DebugLog = false;
        
        // for each type, try to patch all methods
        var methods = testCase.Types.SelectMany(x => x.GetMethods((BindingFlags) (-1))).ToList();
        var methodsThatDidntHaveSamples = new List<MethodInfo>();

        var p = Progress.Start("Injecting Samples");
        int current = 0;
        foreach (var methodInfo in methods)
        {
            current++;
            if(current % 50 == 0)
                Progress.Report(p, current, methods.Count, methodInfo.ToString());
                
            var patchMethod = new TestHelpers.PatchMethod(methodInfo, true);
            yield return patchMethod;

            if (!patchMethod.InjectedSampleNames.Any())
                methodsThatDidntHaveSamples.Add(methodInfo);
            
            var task = SelectiveProfiler.DisableAndForget(methodInfo);
            while (!task.IsCompleted)
                yield return null;
        }

        Progress.Remove(p);

        var allMethodsCount = methods.Count();
        var methodsThatDidntHaveSamplesCount = methodsThatDidntHaveSamples.Count();
        var percentageWithSamples = (float) methodsThatDidntHaveSamplesCount / allMethodsCount * 100f;

        var log = $"Methods without samples ({methodsThatDidntHaveSamplesCount}/{allMethodsCount}, {percentageWithSamples:F2}%): \n{string.Join("\n", methodsThatDidntHaveSamples)}";
        Debug.Log(log);
        
        // We know we can't patch everything here - this might be more suited for another test that only patches methods that we think should succeed
        // CollectionAssert.IsEmpty(methodsThatDidntHaveSamples, log);
        
        SelectiveProfilerSettings.instance.DebugLog = logBefore;
    }

}
