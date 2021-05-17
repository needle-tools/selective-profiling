using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEditor;

namespace Needle.SelectiveProfiling
{
	public static class Patcher
	{
		[InitializeOnLoadMethod]
		private static void Init()
		{
			if (SelectiveProfilerSettings.instance.Enabled) 
				ApplyPatches();
		}

		private static Harmony _harmony;
		private static List<IPatch> patches;
		private static readonly Dictionary<string, IPatch> activePatches = new Dictionary<string, IPatch>();
		private static readonly Dictionary<string, IPatch> knownPatches = new Dictionary<string, IPatch>();

		internal static Harmony Harmony
		{
			get
			{
				if (_harmony == null)
					_harmony = new Harmony("com.needle.selective-profiling");
				return _harmony;
			}
		}

		internal static void ApplyPatches()
		{
			if (patches == null)
			{
				patches = new List<IPatch>();
				var col = TypeCache.GetTypesDerivedFrom(typeof(IPatch));
				foreach (var t in col)
				{
					if (t.IsAbstract) continue;
					try
					{
						var inst = Activator.CreateInstance(t) as IPatch;
						patches.Add(inst);
					}
					catch (MissingMethodException)
					{
						// ignore when patch base has no default constructor we assume it is instantiated in a different way
					}
				}
				
			}

			foreach (var p in patches)
				Apply(p);
		}

		internal static void RemovePatches()
		{
			foreach (var p in patches)
				p.Remove(Harmony);
		}

		internal static void Apply(string id)
		{
			if (knownPatches.ContainsKey(id))
				Apply(knownPatches[id]);
		}

		internal static void Apply(IPatch patch)
		{
			var id = patch.Id;
			if (activePatches.ContainsKey(id)) return;
			activePatches.Add(id, patch);
			if (!knownPatches.ContainsKey(id))
				knownPatches.Add(id, patch);
			patch.Apply(Harmony);
		}

		internal static Task<bool> ApplyAsync(IPatch patch)
		{
			var id = patch.Id;
			if (activePatches.ContainsKey(id)) return Task.FromResult(true);
			activePatches.Add(id, patch);
			if (!knownPatches.ContainsKey(id))
				knownPatches.Add(id, patch);
			patch.Apply(Harmony);
			return Task.FromResult(true);
		}

		internal static void Remove(IPatch patch)
		{
			var id = patch.Id;
			if (!activePatches.ContainsKey(id)) return;
			activePatches.Remove(id);
			patch.Remove(Harmony);
		}

		internal static Task<bool> RemoveAsync(IPatch patch)
		{
			if (!activePatches.ContainsKey(patch.Id)) return Task.FromResult(true);
			activePatches.Remove(patch.Id);
			patch.Remove(Harmony);
			return Task.FromResult(true);
		}

		internal static bool IsActive(IPatch patch)
		{
			return activePatches.ContainsKey(patch.Id);
		}

		internal static bool IsActive(string id)
		{
			return activePatches.ContainsKey(id);
		}
	}
}