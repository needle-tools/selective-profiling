using System;
using System.Collections.Generic;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public class ComponentCreatingLotsOfGarbage : MonoBehaviour
	{
		public bool IsSpecial = false;

		private void Update()
		{
			// if (IsSpecial && !SelectiveProfiler.SpecialObjects.Contains(this)) SelectiveProfiler.SpecialObjects.Add(this);
			// else if (!IsSpecial && SelectiveProfiler.SpecialObjects.Contains(this)) SelectiveProfiler.SpecialObjects.Remove(this);
			
			for (var i = 0; i < 100; i++)
			{
				var some_str = "123" + i;
			}
			for (var i = 0; i < 10; i++) new MyClass();
			for (var i = 0; i < 10; i++) new MyOtherClass();
			var ints = new int[100];
			var str = new string[100];
			var dbls = new double[100];
			var longs = new long[10];
			var shorts = new short[100];
			var classes = new MyClass[100];
			var list = new List<string>();
			var classWithParam = new MyClass(0);
			var generic = new GClass<int>();
			var nestedGeneric = new GClass<GClass<int>>();
			var moreNesting = new ClassWithNesting<string>.AnotherClass<float>();
		}

		private class MyClass
		{
			public MyClass(){}
			public MyClass(int i){}
		}

		private class MyOtherClass{}
		
		private class GClass<T> {}

		private class ClassWithNesting<T>
		{
			public class AnotherClass<U>
			{
				
			}
		}
	}
}