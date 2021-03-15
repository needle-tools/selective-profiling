using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Needle.SelectiveProfiling.Utils;
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
		public bool AutoProfile = false;
		public bool DeepProfiling = false;
		public int MaxDepth = 2;
		
		[SerializeField]
		private List<MethodInformation> Methods = new List<MethodInformation>();
		[SerializeField]
		private List<MethodInformation> Muted = new List<MethodInformation>();

		// [SerializeField]
		// private List<ProfilingConfiguration> Configurations = new List<ProfilingConfiguration>();
		
		public void Add(MethodInformation info)
		{
			if (Methods.Contains(info)) return;
			Methods.Add(info);
			Muted.RemoveAll(m => m.Equals(info));
		}

		public void Remove(MethodInformation info)
		{
			Methods.RemoveAll(m => m.Equals(info));
			Muted.RemoveAll(m => m.Equals(info));
		}

		public void SetMuted(MethodInformation info, bool mute)
		{
			if (mute)
			{
				if (Muted.Contains(info)) return;
				Muted.Add(info);
				Methods.RemoveAll(m => m.Equals(info));
			}
			else Add(info);
		}

		public void ClearAll()
		{
			Methods.Clear();
			Muted.Clear();
		}

		public IReadOnlyList<MethodInformation> MethodsList => Methods;
		public IReadOnlyList<MethodInformation> MutedMethods => Muted;
		public bool IsMuted(MethodInformation m) => Muted.Contains(m);
	}
}