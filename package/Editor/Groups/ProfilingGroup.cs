using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

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
			var openDir = LastSavedPath;
			openDir = Path.GetDirectoryName(openDir);
			
			var path = EditorUtility.SaveFilePanel("Save Methods", openDir, nameof(ProfilingGroup), "asset");
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
		
		[ContextMenu(nameof(AddToProfiled))]
		public void AddToProfiled()
		{
			SelectiveProfilerSettings.instance.Add(methods);
		}
		
		[ContextMenu(nameof(ReplaceProfiled))]
		public void ReplaceProfiled()
		{
			SelectiveProfilerSettings.instance.Replace(methods);
		}

		
		[ContextMenu(nameof(RemoveProfiled))]
		public void RemoveProfiled()
		{
			SelectiveProfilerSettings.instance.Remove(methods);
		}
		
		[FormerlySerializedAs("Methods")] [SerializeField]
		private List<MethodInformation> methods = new List<MethodInformation>();

		public List<MethodInformation> Methods => methods;
		public int MethodsCount => methods.Count;
	}
}