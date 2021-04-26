using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace SelectiveProfilingTestComponent
{
	public class TestConstrainedCall
	{
		private static void DoCall()
		{
			string str = null;
			foreach (var num in Enumerate())
			{
				str = num.ToString();
				var calc = 1 + 2;
				str = TakeAny(str);
			}
		}

		public static T TakeAny<T>(T str)
		{
			return str;
		}

		public static IEnumerable<MyObj> Enumerate()
		{
			return new[] {new MyObj()};
		}

		public class MyObj
		{
			
		}
	}
}