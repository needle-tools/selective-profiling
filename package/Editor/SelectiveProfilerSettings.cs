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
			base.Save(true);
		}

		public bool Enabled = true;
		public bool ImmediateMode = false;
		
		public bool DeepProfiling = false;
		public int MaxDepth = 2;
		
		public bool DebugLog;
		
		// TODO: refactor to config
		[SerializeField]
		private List<MethodInformation> Methods = new List<MethodInformation>();
		[SerializeField]
		private List<MethodInformation> Muted = new List<MethodInformation>();

		public int AllSelectedMethodsCount => Methods.Count + Muted.Count;

		// [SerializeField]
		// private List<ProfilingConfiguration> Configurations = new List<ProfilingConfiguration>();
		
		public void Add(MethodInformation info)
		{
			if (Methods.Contains(info)) return;
			Undo.RegisterCompleteObjectUndo(this, "Add " + info);
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
				Undo.RegisterCompleteObjectUndo(this, "Mute " + info);
				if (Muted.Contains(info)) return;
				Muted.Add(info);
				Methods.RemoveAll(m => m.Equals(info));
			}
			else Add(info);
		}

		public void ClearAll()
		{
			Undo.RegisterCompleteObjectUndo(this, "Clear Selective Profiler Data");
			Methods.Clear();
			Muted.Clear();
		}

		public IReadOnlyList<MethodInformation> MethodsList => Methods;
		public IReadOnlyList<MethodInformation> MutedMethods => Muted;
		public bool IsMuted(MethodInformation m) => Muted.Contains(m);
	}
}