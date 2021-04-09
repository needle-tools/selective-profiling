using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using needle.EditorPatching;
using Needle.SelectiveProfiling.Utils;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

#if !UNITY_2020_1_OR_NEWER
using System.IO;
using System.Linq;
using UnityEditorInternal;
#endif

namespace Needle.SelectiveProfiling
{

	internal static class PinnedItems 
	{
		public static List<string> PinnedProfilerItems => SelectiveProfilerSettings.instance.PinnedMethods;
		// public static List<string> UnpinnedProfilerItems => SelectiveProfilerSettings.instance.UnpinnedMethods;

		public static void ClearPinnedItems()
		{
			PinnedProfilerItems.Clear();
			// UnpinnedProfilerItems.Clear();
		}

		public static void Save() => SelectiveProfilerSettings.instance.Save();
	}

	[FilePath("ProjectSettings/SelectiveProfiler.asset", FilePathAttribute.Location.ProjectFolder)]
	internal class SelectiveProfilerSettings : ScriptableSingleton<SelectiveProfilerSettings>
	{
		private static SelectiveProfilerSettings _settingsFromMainProcess;
		internal static SelectiveProfilerSettings Instance
		{
			get
			{
				if (SelectiveProfiler.IsStandaloneProcess && _settingsFromMainProcess != null)
				{
					Debug.Log("Update settings");
					return _settingsFromMainProcess;
				}
				return instance;
			}
			set
			{
				if (SelectiveProfiler.IsStandaloneProcess) _settingsFromMainProcess = value;
				else throw new NotSupportedException("Updating profiler settings instance is only allowed in standalone profiler process");
			}
		}
		
		[InitializeOnLoadMethod]
		private static void Init()
		{
			if (!Application.isPlaying)
				Undo.undoRedoPerformed += () => instance.Save();

			if (instance.FirstInstall)
			{
				Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "<b>Thank you for installing Selective Profiler Alpha</b>. " +
				                                                           "You should now be able to right click methods in the Unity Profiler to open profiling options.\n\n" +
				                                                           "For more information please read the documentation on github: <b>https://github.com/needle-tools/selective-profiling</b>\n" +
				                                                           "or join us on discord: <b>https://discord.gg/CFZDp4b</b>");
				instance.FirstInstall = false;
				instance.Save();

				foreach (var exp in SelectiveProfiler.ExpectedPatches())
				{
					PatchManager.EnablePatch(exp);
				}
			}
		}

		internal void Save()
		{
			base.Save(true);
		}

		[SerializeField] internal bool FirstInstall = true;

		public bool Enabled = true;
		public bool SkipProperties => true;
		public bool UseAlwaysProfile = false;
		public bool ImmediateMode => false;
		
		public bool CollapseProperties = false;
		public bool CollapseHierarchyNesting = false;
		public bool AllowCollapsing => CollapseProperties || CollapseHierarchyNesting;

		public bool DeepProfiling = false;
		public int MaxDepth = 1;
		public Level DeepProfileMaxLevel = (Level) ~0;

		public bool AllowPinning => SelectiveProfiler.DevelopmentMode;

		public bool DebugLog;

		[SerializeField] private List<MethodInformation> Methods = new List<MethodInformation>();

		[SerializeField] internal List<string> PinnedMethods = new List<string>();
		// [SerializeField] internal List<string> UnpinnedMethods = new List<string>();


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

		internal void RegisterUndo(string actionName) => Undo.RegisterCompleteObjectUndo(this, actionName);

		public void Add(MethodInformation info)
		{
			if (Methods.Any(m => m.Equals(info))) return;
			Undo.RegisterCompleteObjectUndo(this, "Add " + info);
			info.Enabled = true;
			Methods.Add(info);
			MethodStateChanged?.Invoke(info, true);
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
			MethodStateChanged?.Invoke(info, state);
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

		public IReadOnlyList<MethodInformation> MethodsList => Methods;

		// public bool IsMuted(MethodInformation m)
		// {
		// 	var match = Methods.FirstOrDefault(e => e.Equals(m));
		// 	if (match != null) return !match.Enabled;
		// 	return true;
		// }

		public bool IsEnabledExplicitly(MethodInformation mi) => MethodsList.FirstOrDefault(m => m.Equals(mi) && m.Enabled) != null;

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