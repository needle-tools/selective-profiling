using UnityEngine;

namespace SelectiveProfilingTestComponent
{
	public class TestVirt
	{
		private static void DoCall(MyObj<IMyInterface> obj)
		{
			Debug.Log(new MyStruct().ToString());
			// var res = obj.Run();
			// Debug.Log(res);
			// string str = null;
			// foreach (var num in Enumerate())
			// {
			// 	// str = num.ToString();
			// 	// var calc = 1 + 2;
			// 	// str = TakeAny(str);
			// }
		}

		// public static T TakeAny<T>(T str)
		// {
		// 	return str;
		// }
		//
		// public static IEnumerable<MyObj<string>> Enumerate()
		// {
		// 	return new[] {new MyObj<string>()};
		// }

		public abstract class MyObj<T>
		{
			public T Run()
			{
				return default;
			}
		}
		
		public interface IMyInterface
		{
			
		}

		public struct MyStruct
		{
			
		}

		public class MyObjImpl : MyObj<string>
		{
			
		}
	}
}