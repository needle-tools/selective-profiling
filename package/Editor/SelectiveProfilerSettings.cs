using System.Collections.Generic;
using UnityEditor;

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

		internal List<ProfilingInfo> Entries = new List<ProfilingInfo>();
	}
}