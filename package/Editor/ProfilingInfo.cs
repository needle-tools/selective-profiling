using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
using Needle.SelectiveProfiling.Utils;
using UnityEditor.Profiling;

namespace Needle.SelectiveProfiling
{
	internal class ProfilingInfo
	{
		public readonly EditorPatchProvider Patch;
		public readonly MethodInfo Method;
		public readonly MethodInformation Data;

		public bool IsActive => Patch != null && PatchManager.IsActive(Patch.ID());
		internal string Identifier => Method?.GetMethodIdentifier();

		public ProfilingInfo(EditorPatchProvider patch, MethodInfo info, MethodInformation data)
		{
			this.Patch = patch;
			this.Method = info;
			this.Data = data;
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
	}
}