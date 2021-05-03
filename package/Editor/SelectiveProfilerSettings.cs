using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

	[FilePath(SettingsRelativeFilePath, FilePathAttribute.Location.ProjectFolder)]
	internal class SelectiveProfilerSettings : ScriptableSingleton<SelectiveProfilerSettings>
	{
		public const string SettingsRelativeFilePath = "ProjectSettings/SelectiveProfiler.asset";
		
		[MenuItem(MenuItems.ToolsMenu + "Copy settings to clipboard")]
		public static bool CopySettingsToClipboard()
		{
			if (!File.Exists(SettingsRelativeFilePath)) return false;
			GUIUtility.systemCopyBuffer = File.ReadAllText(SettingsRelativeFilePath);
			return true;
		}
		
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
		public bool CollapseNoImpactSamples = false;
		public bool AllowCollapsing => CollapseProperties || CollapseHierarchyNesting || CollapseNoImpactSamples;
		public bool ColorPerformanceImpact = true;

		public bool DeepProfiling = false;
		public int MaxDepth = 1;
		public Level DeepProfileMaxLevel = (Level) ~0;

		public bool AllowPinning => SelectiveProfiler.DevelopmentMode;

		public bool DebugLog;
		
		[SerializeField] internal List<string> PinnedMethods = new List<string>();
		
		[SerializeField]
		private ProfilingGroup CurrentGroup;

		public bool HasGroup => CurrentGroup;
		public string GroupName => CurrentGroup ? CurrentGroup.name : null;
		
		public bool IsEnabled(ProfilingGroup group) => group == CurrentGroup;
		
		public void SetGroup(ProfilingGroup group)
		{
			Debug.Log("Set group: " + group);
			if (group == CurrentGroup) return;
			
			if (CurrentGroup)
			{
				CurrentGroup.MethodStateChanged -= MethodStateChanged;
				CurrentGroup.Cleared -= Cleared;
			}

			var old = CurrentGroup;
			CurrentGroup = group;
			if (CurrentGroup)
			{
				CurrentGroup.MethodStateChanged += MethodStateChanged;
				CurrentGroup.Cleared += Cleared;
			}

			GroupChanged?.Invoke((old, CurrentGroup));
		}

		public static event Action<(ProfilingGroup old, ProfilingGroup @new)> GroupChanged;
		public static event Action<MethodInformation, bool> MethodStateChanged;
		public static event Action Cleared;

		
		internal void RegisterUndo(string actionName) => Undo.RegisterCompleteObjectUndo(this, actionName);

		public int MethodsCount => CurrentGroup ? CurrentGroup.MethodsCount : 0;


		public MethodInformation GetInstance(MethodInformation mi)
		{
			if (CurrentGroup)
				return CurrentGroup.GetInstance(mi);

			return mi;
		}

		public void Add(MethodInformation info)
		{
			if (CurrentGroup) CurrentGroup.Add(info);
		}

		public void Remove(MethodInfo info, bool withUndo = true)
		{
			if (CurrentGroup)
			{
				CurrentGroup.Remove(info, withUndo);
			}
		}

		public void Remove(MethodInformation info, bool withUndo = true)
		{
			if (CurrentGroup)
			{
				CurrentGroup.Remove(info, withUndo);
			}
		}

		public void UpdateState(MethodInformation info, bool state, bool withUndo)
		{
			if (CurrentGroup)
			{
				CurrentGroup.UpdateState(info, state, withUndo);
			}
		}

		public void SetMuted(MethodInformation info, bool mute, bool withUndo = true)
		{
			UpdateState(info, !mute, withUndo);
		}

		public bool IsSavedAndEnabled(MethodInformation mi)
		{
			if (CurrentGroup) return CurrentGroup.IsSavedAndEnabled(mi);
			return false;
		}

		public bool Contains(MethodInfo info)
		{
			if (CurrentGroup) return CurrentGroup.Contains(info);
			return false;
		}

		public IReadOnlyList<MethodInformation> MethodsList => CurrentGroup ? CurrentGroup.Methods : null;
		
		public bool IsEnabledExplicitly(MethodInformation mi) => 
			MethodsList != null && MethodsList.FirstOrDefault(m => m.Equals(mi) && m.Enabled) != null;

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