using System;
using System.Threading;
using MyNamespace;
using Needle.SelectiveProfiling.Attributes;
using UnityEngine;

namespace DefaultNamespace
{
	public class ClassWithProfiledMethod : MonoBehaviour
	{
		private void Update()
		{
			ThisMethodShouldAlwaysBeProfiled();
		}

		[AlwaysProfile]
		private void ThisMethodShouldAlwaysBeProfiled()
		{
			SomeCall();
		}

		private void SomeCall()
		{
			
		}
	}
}