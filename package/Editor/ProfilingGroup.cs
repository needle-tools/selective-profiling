using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	[CreateAssetMenu(menuName = "Profiling/" + nameof(ProfilingGroup), fileName = nameof(ProfilingGroup), order = 0)]
	public class ProfilingGroup : ScriptableObject
	{
		[ContextMenu(nameof(Enable))]
		private void Enable()
		{
			SelectiveProfilerSettings.instance.SetGroup(this);
		}
		[ContextMenu(nameof(Disable))]
		private void Disable()
		{
			SelectiveProfilerSettings.instance.SetGroup(null);
		}

		private void NotifyStateChanged(MethodInformation info, bool state)
		{
			MethodStateChanged?.Invoke(info, state);
		}

		public event Action<MethodInformation, bool> MethodStateChanged;
		public event Action Cleared;

		public List<MethodInformation> Methods = new List<MethodInformation>();
		public int MethodsCount => Methods.Count;


		public MethodInformation GetInstance(MethodInformation mi)
		{
			foreach (var m in Methods)
			{
				if (m.Equals(mi))
					return m;
			}

			return mi;
		}

		public void Add(MethodInformation info)
		{
			if (Methods.Any(m => m.Equals(info))) return;
			Undo.RegisterCompleteObjectUndo(this, "Add " + info);
			info.Enabled = true;
			Methods.Add(info);
			NotifyStateChanged(info, true);
		}

		public void Remove(MethodInfo info, bool withUndo = true)
		{
			InternalRemove(info.Name, entry => entry.Equals(info), withUndo);
		}

		public void Remove(MethodInformation info, bool withUndo = true)
		{
			InternalRemove(info.Method, entry => entry.Equals(info), withUndo);
		}

		private void InternalRemove(string id, Predicate<MethodInformation> pred, bool withUndo)
		{
			if (withUndo)
				Undo.RegisterCompleteObjectUndo(this, "Removed " + id + "/" + this);

			MethodInformation removed = null;
			for (var index = Methods.Count - 1; index >= 0; index--)
			{
				var method = Methods[index];
				if (!pred(method)) continue;
				Methods.RemoveAt(index);
				removed = method;
				break;
			}

			if (removed != null)
			{
				NotifyStateChanged(removed, false);
			}
		}

		public void UpdateState(MethodInformation info, bool state, bool withUndo)
		{
			if (info.Enabled == state) return;
			if (withUndo) Undo.RegisterCompleteObjectUndo(this, "Set " + info + ": " + state);
			info.Enabled = state;
			NotifyStateChanged(info, state);
		}

		public void SetMuted(MethodInformation info, bool mute, bool withUndo = true)
		{
			UpdateState(info, !mute, withUndo);
		}

		public bool IsSavedAndEnabled(MethodInformation mi)
		{
			var m = Methods.FirstOrDefault(entry => entry.Equals(mi));
			return m?.Enabled ?? false;
		}

		public void ClearAll()
		{
			Undo.RegisterCompleteObjectUndo(this, "Clear Selective Profiler Data");
			Methods.Clear();
			Cleared?.Invoke();
		}

		public bool Contains(MethodInfo info)
		{
			return Methods.Any(m => m.Equals(info));
		}

		public bool IsEnabled(MethodInformation mi) => Methods.FirstOrDefault(m => m.Equals(mi) && m.Enabled) != null;
	}
}