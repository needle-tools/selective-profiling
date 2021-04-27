using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;

public class SomeSpecialMarker : MonoBehaviour
{
    private void Update()
    {
        if (Random.value < .0001f) 
        {
            Profiler.BeginSample("MySpecialSample");
            Thread.Sleep(1);
            Profiler.EndSample();
        }
    }

    public void DoSomething()
    {
        Profiler.BeginSample("MyOtherSample");
        Thread.Sleep(3);
        Profiler.EndSample();
    }
}
