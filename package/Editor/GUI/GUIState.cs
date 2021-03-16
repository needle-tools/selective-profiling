using UnityEditor;

namespace Needle.SelectiveProfiling
{
	internal static class GUIState
	{
		// public static bool SelectedMethodsFoldout
		// {
		// 	get => SessionState.GetBool(nameof(SelectedMethodsFoldout), false);
		// 	set => SessionState.SetBool(nameof(SelectedMethodsFoldout), value);
		// }
		//
		// public static bool DebugOptionsFoldout
		// {
		// 	get => SessionState.GetBool(nameof(DebugOptionsFoldout), false);
		// 	set => SessionState.SetBool(nameof(DebugOptionsFoldout), value);
		// }
		//
		// public static bool ActivePatchesFoldout
		// {
		// 	get => SessionState.GetBool(nameof(ActivePatchesFoldout), false);
		// 	set => SessionState.SetBool(nameof(ActivePatchesFoldout), value);
		// }
		
		
		public static bool MethodsListFoldout
		{
			get => SessionState.GetBool(nameof(MethodsListFoldout), false);
			set => SessionState.SetBool(nameof(MethodsListFoldout), value);
		}
		
		public static bool MutedMethodsFoldout
		{
			get => SessionState.GetBool(nameof(MutedMethodsFoldout), false);
			set => SessionState.SetBool(nameof(MutedMethodsFoldout), value);
		}
		
		public static bool ScopesListFoldout
		{
			get => SessionState.GetBool(nameof(ScopesListFoldout), false);
			set => SessionState.SetBool(nameof(ScopesListFoldout), value);
		}
		
		internal static MethodScopeDisplay SelectedScope
		{
			get => (MethodScopeDisplay)SessionState.GetInt("SelectedScopeDisplay", (int) (MethodScopeDisplay.Type));
			set => SessionState.SetInt("SelectedScopeDisplay", (int) value);
		}

	}
}