using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
using Needle.SelectiveProfiling.Utils;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	// [AlwaysProfile]
	internal class ProfilingInfo
	{
		public readonly EditorPatchProvider Patch;
		public readonly MethodInfo Method;
		public MethodInformation MethodInformation;

		public bool IsActive => Patch != null && PatchManager.IsActive(Patch.ID());
		internal string Identifier => Method?.GetMethodIdentifier();

		private bool enabled;

		public ProfilingInfo(EditorPatchProvider patch, MethodInfo info, MethodInformation mi)
		{
			this.Patch = patch;
			this.Method = info;
			this.MethodInformation = mi.Copy();
		}

		public Task Enable(bool force = false)
		{
			if (!force && !MethodInformation.Enabled)
				return Task.CompletedTask;
			MethodInformation.Enabled = true;
			var ts = Patch.Enable(false);
			if (!enabled)
			{
				enabled = true;
				OnEnable();
				
#if UNITY_2020_2_OR_NEWER
				if (!SelectiveProfiler.IsStandaloneProcess)
				{
					SelectiveProfiler.QueueCommand(new ProfiledMethodStateChanged(MethodInformation, true));
				}
#endif
			}

			return ts;
		}

		public Task Disable()
		{
			MethodInformation.Enabled = false;
			var t = Patch.Disable(true, false);
			if (enabled)
			{
				enabled = false;
				OnDisable();
				
#if UNITY_2020_2_OR_NEWER
				if (!SelectiveProfiler.IsStandaloneProcess)
				{
					SelectiveProfiler.QueueCommand(new ProfiledMethodStateChanged(MethodInformation, false));
				}
#endif
			}
			return t;
		}

		public void ToggleActive()
		{
			if (IsActive) Disable();
			else Enable();
		}

		public override string ToString()
		{
			return Patch?.ID() + " - " + Identifier;
		}

		private HashSet<ProfilingInfo> callers;
		private HashSet<ProfilingInfo> callees;

		private static readonly HashSet<ProfilingInfo> callstack = new HashSet<ProfilingInfo>();

		private bool HasExplicitlyEnabledCaller(ISet<ProfilingInfo> stack)
		{
			if (stack.Contains(this)) return false;
			var settings = SelectiveProfilerSettings.instance;
			if (settings.IsEnabledExplicitly(MethodInformation)) return true;
			stack.Add(this);
			if (callers != null)
			{
				foreach (var c in callers)
				{
					if (c == this) throw new Exception("Caller can not be self " + this);
					if (c.HasExplicitlyEnabledCaller(stack)) return true;
				}
			}

			return false;
		}

		internal void AddCaller(ProfilingInfo caller)
		{
			if (caller == null) return;
			if (caller == this) return;
			if (callers == null) callers = new HashSet<ProfilingInfo>();

			if (!callers.Contains(caller))
			{
				callers.Add(caller);
				if (SelectiveProfiler.DebugLog)
					Debug.Log(Method.Name + " called by\n" + string.Join("\n", callers.Select(c => c.Method.Name)));
				caller.AddCallee(this);
			}
		}

		private void RemoveCaller(ProfilingInfo caller)
		{
			if (callers == null) return;
			if (!callers.Contains(caller)) return;
			callers.Remove(caller);
			// check if this method is still called by anyone
			callstack.Clear();
			if (callers.Count <= 0 || !HasExplicitlyEnabledCaller(callstack))
			{
				// if not check if is explicitly enabled
				if (!SelectiveProfilerSettings.instance.IsEnabledExplicitly(MethodInformation))
					Disable();
			}
		}

		private void AddCallee(ProfilingInfo callee)
		{
			if (callee == null) return;
			if (callee == this) return;
			if (callees == null) callees = new HashSet<ProfilingInfo>();
			else if (callees.Contains(callee)) return;
			callees.Add(callee);
		}

		private void OnEnable()
		{
			if (SelectiveProfilerSettings.instance.DeepProfiling && callees != null)
			{
				foreach (var cl in callees)
				{
					cl.AddCaller(this);
					cl.Enable();
				}
			}
		}

		private void OnDisable()
		{
			if (SelectiveProfiler.DebugLog)
				Debug.Log("Disabled " + this);
			if (callees != null)
			{
				if (SelectiveProfiler.DebugLog)
					Debug.Log(callees.Count + " callees\n" + string.Join("\n", callees.Select(c => c)));
				foreach (var cl in callees)
				{
					cl.RemoveCaller(this);
				}
			}
		}
	}
}