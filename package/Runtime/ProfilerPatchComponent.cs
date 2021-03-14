using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Needle.SelectiveProfiling;
using UnityEngine;

#if UNITY_EDITOR
using HarmonyLib;
using needle.EditorPatching;
#endif

namespace DefaultNamespace
{
	public class ProfilerPatchComponent : MonoBehaviour
	{
		private readonly List<ProfilerSamplePatch> comp = new List<ProfilerSamplePatch>();

		public Component ToProfile;
		public bool EnableAll;

#if UNITY_EDITOR
		private void OnEnable()
		{
			if (!ToProfile) return;
			
			var methods = ToProfile.GetType().GetMethods((BindingFlags) ~0)
				.Where(m => m.HasMethodBody())
				.ToArray();
			foreach (var m in methods)
			{
				// if (m.Name != "Update") continue;
				var p = new ProfilerSamplePatch(m, null, " INJECTED");
				PatchManager.RegisterPatch(p);
				comp.Add(p);
				if(EnableAll)
					PatchManager.EnablePatch(p);
			}
		}

		private void OnDisable()
		{
			foreach (var p in comp)
			{
				p.Disable();
			}
		}
#endif
	}
}