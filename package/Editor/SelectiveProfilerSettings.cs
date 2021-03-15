using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

		public bool Enabled = true;
		public bool DebugLog;
		public bool DeepProfiling = false;
		
		[SerializeField]
		private List<MethodInformation> Methods = new List<MethodInformation>();

		// [SerializeField]
		// private List<ProfilingConfiguration> Configurations = new List<ProfilingConfiguration>();

		public void Add(MethodInfo method)
		{
			var info = new MethodInformation(method);
			if (Methods.Contains(info))
			{
				// Debug.Log("Already found " + method);
				return;
			}
			this.Methods.Add(info);
		}

		public IList<MethodInformation> MethodsList => Methods;
	}
}