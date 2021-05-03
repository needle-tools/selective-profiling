#if UNITY_EDITOR

using Needle.SelectiveProfiling;
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
#endif