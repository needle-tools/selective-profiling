using System;
using MyNamespace;
using Needle.SelectiveProfiling.Attributes;
using UnityEngine;

namespace DefaultNamespace
{
	[AlwaysProfile]
	public class ThisIsAlwaysProfiled : MonoBehaviour
	{
		private void Update()
		{
			AnotherMethod();
		}

		public void AnotherMethod()
		{
			SomeCall();
		}

		private void SomeCall(){}
	}
}