using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;

public class ThreadSleep : MonoBehaviour
{
    // private void Update()
    // {
    //     if (Random.value < .0001f) 
    //     {
    //         Profiler.BeginSample("MySpecialSample");
    //         Thread.Sleep(1);
    //         Profiler.EndSample();
    //     }
    // }

    public void Sleep100()
    {
        // Profiler.BeginSample("MyOtherSample");
        Thread.Sleep(100);
        // Profiler.EndSample();
    }
}
