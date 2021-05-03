using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;

public class CallMethodWithProfilingSampler : MonoBehaviour
{
	private CustomSampler sampler;

	private void Start()
	{
		sampler = CustomSampler.Create("MySampler");
	}

	private void Update()
	{
		using (new Disp(sampler))
		{
			for (int i = 0; i < 1; i++)
				Thread.Sleep(1);
		}
		
		// // using (new Disp(sampler))
		// {
		// 	sampler.Begin();
		// 	for (int i = 0; i < 1; i++)
		// 		Thread.Sleep(1);
		// 	sampler.End();
		// }
	}

	private class Disp : IDisposable
	{
		private CustomSampler sampler;

		public Disp(CustomSampler sampler)
		{
			this.sampler = sampler;
			sampler.Begin();
		}


		public void Dispose()
		{
			// sampler.End();
		}
	}
}