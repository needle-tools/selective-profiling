using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MyNamespace
{
	public class ComponentWithRaycasts : MonoBehaviour
	{
		private RaycastHit[] _hitBuffer;

		void Update()
		{
			Debug.Log("Raycast");
			var hits = Physics.RaycastNonAlloc(new Ray(Vector3.zero, Vector3.up).InverseTransformDir(transform), _hitBuffer, 1000, LayerMask.GetMask("Default"));
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
