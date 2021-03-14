using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	[FilePath("ProjectSettings/SelectiveProfiler.asset", FilePathAttribute.Location.ProjectFolder)]
	internal class SelectiveProfilerSettings : ScriptableSingleton<SelectiveProfilerSettings>
	{
		internal void Save()
		{
			Undo.RegisterCompleteObjectUndo(this, "Save Selective Profiler Settings");
			base.Save(true);
		}

		public bool DeepProfiling = false;
		public List<ProfiledMethod> Methods = new List<ProfiledMethod>();

	}
}