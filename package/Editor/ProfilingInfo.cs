using System;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
using Needle.SelectiveProfiling.Utils;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	[Serializable]
	internal class ProfilingInfo : ISerializationCallbackReceiver
	{
		public string Identifier;
		
		[NonSerialized]
		public EditorPatchProvider Patch;
		[NonSerialized]
		public MethodInfo Method;
		public bool IsActive => PatchManager.IsActive(Patch.ID());

		public ProfilingInfo(string identifier)
		{
			
		}

		public ProfilingInfo(EditorPatchProvider patch, MethodInfo info)
		{
			this.Patch = patch;
			this.Method = info;
			this.Identifier = info.GetMethodIdentifier();
		}

		public void ToggleActive()
		{
			if (IsActive) Disable();
			else Enable();
		}

		public Task Enable() => Patch.Enable();
		public void Disable() => Patch.Disable();

		public override string ToString()
		{
			return Patch?.ID() + " - " + Identifier;
		}

		public void OnBeforeSerialize()
		{
			Debug.Log("Before serialize");
		}

		public void OnAfterDeserialize()
		{
			
		}
	}
}