using System;
using Microsoft.CodeAnalysis.CSharp;
using Needle.SelectiveProfiling.CodeWrapper;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	[CustomEditor(typeof(ProfilingGroup))]
	public class ProfilingGroupEditor : Editor
	{
		private ReorderableList list;
		//
		// private void OnEnable()
		// {
		// 	var group = target as ProfilingGroup;
		// 	if (group == null) return;
		// 	var content = group.Methods;
		// 	list = new ReorderableList(content, typeof(MethodInformation));
		// 	list.drawHeaderCallback += rect => GUI.Label(rect, "Group Content [" + group.MethodsCount + "]", EditorStyles.boldLabel); 
		// 	list.drawElementCallback += (rect, index, active, focused) =>
		// 	{
		// 		var entry = content[index];
		// 		if (entry.TryResolveMethod(out var method))
		// 		{
		// 			var label = TranspilerUtils.GetNiceMethodName(method, false);
		// 			GUI.Label(rect, label);
		// 			return;
		// 		}
		// 		
		// 		GUI.Label(rect, "MISSING: " + entry.Type + "::" + entry.Method);
		// 	};
		// 	list.onCanRemoveCallback += reorderableList => true;
		// 	list.onRemoveCallback += reorderableList =>
		// 	{
		// 		Undo.RegisterCompleteObjectUndo(group, "Remove " + content[reorderableList.index].ClassWithMethod());
		// 		content.RemoveAt(reorderableList.index);
		// 	};
		// }

		public override void OnInspectorGUI()
		{
			// using (new EditorGUI.DisabledScope(true))
			// 	base.OnInspectorGUI();

			var group = target as ProfilingGroup;
			if (group == null) return;

			if (GUILayout.Button("Add"))
			{
				group.AddToProfiled();
			}

			if (GUILayout.Button("Remove"))
			{
				group.RemoveProfiled();
			}

			if (GUILayout.Button("Replace"))
			{
				group.ReplaceProfiled();
			}

			GUILayout.Space(5);

			if (list != null)
			{
				list.DoLayoutList();
			}
			else
			{
				EditorGUILayout.LabelField("Group Content", EditorStyles.boldLabel);
				Draw.ScopesList(
					group.Methods,
					e => e.Enabled, null, null,
					rem =>
					{
						Undo.RegisterCompleteObjectUndo(group, $"Remove {rem.Count} methods in {@group.name}");
						foreach (var e in rem)
							group.Methods.Remove(e);
					},
					new Draw.ScopeOptions()
					{
						HideToggle = true,
						BoldScopeHeader = true,
					});
			}
		}
	}
}