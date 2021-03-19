using System;
using System.Collections.Generic;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public class ComponentCreatingLotsOfGarbage : MonoBehaviour
	{
		private void Update()
		{
			for (var i = 0; i < 10; i++) new MyClass();
			for (var i = 0; i < 10; i++) new MyOtherClass();

			var ints = new int[100];
			var str = new string[100];
			var dbls = new double[100];
			var longs = new long[10];
			var shorts = new short[100];
			var classes = new MyClass[100];
			var list = new List<string>();
			var onlyAString = "some string";
		}

		private class MyClass {}

		private class MyOtherClass{}
	}
}