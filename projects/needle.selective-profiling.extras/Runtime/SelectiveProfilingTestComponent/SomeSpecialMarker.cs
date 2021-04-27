using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;

public class SomeSpecialMarker : MonoBehaviour
{
    private void Update()
    {
        if (Time.frameCount % 300 == 0 || Random.value < .01f) 
        {
            Profiler.BeginSample("MySpecialSample");
            Thread.Sleep(1);
            Profiler.EndSample();
        }
    }
}
