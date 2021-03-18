using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace MyNamespace
{
	public class ComponentWithRaycasts : MonoBehaviour
	{
		private RaycastHit[] _hitBuffer;

		void Update()
		{
			var ray = new Ray(Vector3.zero, Vector3.up).InverseTransformDir(transform);
			var hits = Physics.RaycastNonAlloc(ray, _hitBuffer, 1000, LayerMask.GetMask("Default"));
			Debug.Assert(Compare());
		}

		bool Compare()
		{
			var res = new Vector2(0, 0).Equals(new Vector2(0, 0));
			return res;
		}
	}
	public static class RaycastExtensions
	{
		public static Ray InverseTransformDir(this Ray ray, Transform t)
		{
			ray.direction = t.InverseTransformDirection(ray.direction).normalized;
			return ray;
		}
	}

}
