using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Serialization;
using File = UnityEngine.Windows.File;

namespace Needle.SelectiveProfiling
{
	[CreateAssetMenu(menuName = "Profiling/" + nameof(ProfilingGroup), fileName = nameof(ProfilingGroup), order = 0)]
	public class ProfilingGroup : ScriptableObject
	{
		private static string LastSavedPath
		{
			get
			{
				var path = EditorPrefs.GetString(nameof(ProfilingGroup) + "_LastSavedPath");
				if (string.IsNullOrEmpty(path))
					path = Application.dataPath;
				return path;
			}
			set => EditorPrefs.SetString(nameof(ProfilingGroup) + "_LastSavedPath", value);
		}

		public static ProfilingGroup Save(IEnumerable<MethodInformation> methods)
		{
			Debug.Log(LastSavedPath);
			var path = EditorUtility.SaveFilePanel("Save Methods", LastSavedPath, nameof(ProfilingGroup), "asset");
			if (string.IsNullOrEmpty(path)) return null;

			path = path.Replace("\\", "/");
			var projPath = Path.GetFullPath(Application.dataPath + "/../").Replace("\\", "/");
			if (!path.StartsWith(projPath))
			{
				Debug.LogError("Can only save in project: " + projPath);
				return null;
			}
			path = path.Substring(projPath.Length);
			
			if (File.Exists(path))
			{
				var inst = AssetDatabase.LoadAssetAtPath<ProfilingGroup>(path);
				if (inst)
				{
					LastSavedPath = path;
					inst.Methods.Clear();
					if (methods != null) inst.Methods.AddRange(methods);
					return inst;
				}

				Debug.LogWarning("Could not save methods to " + path);
			}
			else
			{
				LastSavedPath = path;
				var inst = CreateInstance<ProfilingGroup>();
				inst.name = Path.GetFileNameWithoutExtension(path);
				if (methods != null) inst.methods = new List<MethodInformation>(methods);
				AssetDatabase.CreateAsset(inst, path);
				return inst;
			}

			return null;
		}

		[ContextMenu(nameof(SaveTo))]
		private void SaveTo()
		{
			Save(Methods);
		}
		
		[ContextMenu(nameof(Enable))]
		private void Enable()
		{
			SelectiveProfilerSettings.instance.Add(methods);
		}
		
		[ContextMenu(nameof(Replace))]
		private void Replace()
		{
			SelectiveProfilerSettings.instance.Replace(methods);
		}

		
		[ContextMenu(nameof(Disable))]
		private void Disable()
		{
			SelectiveProfilerSettings.instance.Remove(methods);
		}
		
		[FormerlySerializedAs("Methods")] [SerializeField]
		private List<MethodInformation> methods = new List<MethodInformation>();

		public List<MethodInformation> Methods => methods;
		public int MethodsCount => methods.Count;
	}
}