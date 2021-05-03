using System;
using UnityEngine;

namespace SelectiveProfilingTestComponent
{
	public class LoadArgsAndProperty : MonoBehaviour
	{
		private void Update()
		{
			Run();
		}

		private void Run()
		{
			try
			{
				MyMethodToTest("test", 0);
			}
			catch (Exception e)
			{
				
			}
		}

		public void MyMethodToTest(string myArg0, int myValue)
		{
			CallingSomeOtherMethod(GetFrame, myValue, myArg0);
			CallingSomeOtherMethod(myArg0, GetString);
		}

		private void CallingSomeOtherMethod(int frame, int someValue2, string arg)
		{
			Debug.Assert(arg == "test");
			Debug.Assert(frame == Time.frameCount);
			Debug.Assert(someValue2 == 0);
			// throw new Exception("Exit");
		}

		private void CallingSomeOtherMethod(string arg0, string arg1)
		{
			Debug.Assert(arg0 == "test");
			Debug.Assert(arg1 == GetString);
		}

		private string GetString => "getStringResult";

		private int GetFrame
		{
			get
			{
				return Time.frameCount;
			}
		}
	}
}