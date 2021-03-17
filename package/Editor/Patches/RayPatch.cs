using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	[HarmonyPatch(typeof(Ray))]
	public class RayPatch
	{

		[HarmonyPostfix]
		[HarmonyPatch(MethodType.Getter)]
		[HarmonyPatch("origin")]
		public static void Postfix()
		{
			// var r = new Ray();
			// r.origin
			Debug.Log("Get origin " + System.Reflection.MethodBase.GetCurrentMethod().Name);
		}
	}
}