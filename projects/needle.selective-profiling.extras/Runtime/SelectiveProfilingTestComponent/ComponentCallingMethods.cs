using System;
using UnityEngine;

namespace DefaultNamespace
{
	public class ComponentCallingMethods : MonoBehaviour
	{
		private void Update()
		{
			MethodWithStringParameter("test");
			MethodWithGeneric(0);
			MethodMultipleGenerics(0, "test");
			MethodWithTuple(new Tuple<string, int>("test", 0));
			MethodWithTupleShort(("test", 42));
			MethodWithGeneric(new ClassWithGenerics<string, int>());
			SomeNestedClass.SomeMethod();
			MethodCallingOtherMethods();
		}

		private void MethodCallingOtherMethods()
		{
			MethodWithStringParameter("hello");
			MethodWithTupleShort(("123", 123));
		}
		

		private void MethodWithStringParameter(string test)
		{
			var arr = new int[1];
		}

		private void MethodWithGeneric<T>(T someGeneric)
		{
			var arr = new int[1];
		}

		private void MethodMultipleGenerics<T, U>(T myT, U myU)
		{
			var arr = new int[1];
		}

		private void MethodWithTuple(Tuple<string, int> myTuple)
		{
			var arr = new int[1];
		}

		private void MethodWithTupleShort((string, int) test)
		{
			var arr = new int[1];
		}

		private static class SomeNestedClass
		{
			public static void SomeMethod()
			{
				var arr = new int[1];
			}
		}
	}

	public class ClassWithGenerics<T, U>
	{
	}
}