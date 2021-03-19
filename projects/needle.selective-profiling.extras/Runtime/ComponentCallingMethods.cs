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
			MethodWithGeneric(new ClassWithGenerics<string, int>());
			SomeNestedClass.SomeMethod();
		}

		private void MethodWithStringParameter(string test)
		{
			
		}

		private void MethodWithGeneric<T>(T someGeneric)
		{
			
		}
		
		private void MethodMultipleGenerics<T, U>(T myT, U myU){}

		private void MethodWithTuple(Tuple<string, int> myTuple){}

		private static class SomeNestedClass
		{
			public static void SomeMethod(){}
		}
	}

	public class ClassWithGenerics<T, U>
	{
		
	}
}