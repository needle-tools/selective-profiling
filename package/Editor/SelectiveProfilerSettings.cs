using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if !UNITY_2020_1_OR_NEWER
using System.IO;
using System.Linq;
using UnityEditorInternal;
#endif

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
			MethodStateChanged?.Invoke(info, true);
		}

		public void Remove(MethodInformation info)
		{
			for (var index = Methods.Count - 1; index >= 0; index--)
			{
				var method = Methods[index];
				if (method.Equals(info)) continue;
				Methods.RemoveAt(index);
				NotifyStateChanged(method, false);
				break;
			}
			for (var index = Muted.Count - 1; index >= 0; index--)
			{
				var method = Muted[index];
				if (method.Equals(info)) continue;
				Muted.RemoveAt(index);
				NotifyStateChanged(method, false);
				break;
			}
		}

		public void SetMuted(MethodInformation info, bool mute)
		{
			if (mute)
			{
				Undo.RegisterCompleteObjectUndo(this, "Mute " + info);
				if (Muted.Contains(info)) return;
				Muted.Add(info);
				Methods.RemoveAll(m => m.Equals(info));
				MethodStateChanged?.Invoke(info, false);
			}
			else Add(info);
		}

		public void ClearAll()
		{
			Undo.RegisterCompleteObjectUndo(this, "Clear Selective Profiler Data");
			Methods.Clear();
			Muted.Clear();
			Cleared?.Invoke();
		}

		public IReadOnlyList<MethodInformation> MethodsList => Methods;
		public IReadOnlyList<MethodInformation> MutedMethods => Muted;
		public bool IsMuted(MethodInformation m) => Muted.Contains(m);

		public static event Action<MethodInformation, bool> MethodStateChanged;
		public static event Action Cleared;

		private static void NotifyStateChanged(MethodInformation info, bool state)
		{
			MethodStateChanged?.Invoke(info, state);
		}
	}
	
#if !UNITY_2020_1_OR_NEWER
    public class ScriptableSingleton<T> : ScriptableObject where T : ScriptableObject
    {
        private static T s_Instance;

        public static T instance
        {
            get
            {
                if (!s_Instance) CreateAndLoad();
                return s_Instance;
            }
        }

        protected ScriptableSingleton()
        {
            if (s_Instance)
                Debug.LogError("ScriptableSingleton already exists. Did you query the singleton in a constructor?");
            else
            { 
                object casted = this;
                s_Instance = casted as T;
                System.Diagnostics.Debug.Assert(s_Instance != null); 
            }
        }

        private static void CreateAndLoad()
        {
            System.Diagnostics.Debug.Assert(s_Instance == null);

            // Load
            var filePath = GetFilePath();
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(filePath))
                InternalEditorUtility.LoadSerializedFileAndForget(filePath);
#endif
            if (s_Instance == null)
            {
                var t = CreateInstance<T>();
                t.hideFlags = HideFlags.HideAndDontSave;
            }

            System.Diagnostics.Debug.Assert(s_Instance != null);
        }

        protected virtual void Save(bool saveAsText)
        {
            if (!s_Instance)
            {
                Debug.Log("Cannot save ScriptableSingleton: no instance!");
                return;
            }

            var filePath = GetFilePath();
            if (string.IsNullOrEmpty(filePath)) return;
            var folderPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(folderPath) && !string.IsNullOrEmpty(folderPath))
                Directory.CreateDirectory(folderPath);
#if UNITY_EDITOR
            InternalEditorUtility.SaveToSerializedFileAndForget(new[] {s_Instance}, filePath, saveAsText);
#endif
        }

        private static string GetFilePath()
        {
            var type = typeof(T);
            var attributes = type.GetCustomAttributes(true);
            return attributes.OfType<FilePathAttribute>()
                .Select(f => f.filepath)
                .FirstOrDefault();
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    internal sealed class FilePathAttribute : System.Attribute
    {
        public enum Location
        {
            PreferencesFolder,
            ProjectFolder
        }

        public string filepath { get; set; }

        public FilePathAttribute(string relativePath, FilePathAttribute.Location location)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                Debug.LogError("Invalid relative path! (its null or empty)");
                return;
            }

            if (relativePath[0] == '/')
                relativePath = relativePath.Substring(1);
#if UNITY_EDITOR
            if (location == FilePathAttribute.Location.PreferencesFolder)
                this.filepath = InternalEditorUtility.unityPreferencesFolder + "/" + relativePath;
            else
#endif
                this.filepath = relativePath;
        }
    }
#endif
}