using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[ExecuteInEditMode]
public class RepaintEverythingEveryFrame : MonoBehaviour
{
	public bool EditorUpdate;

	private void OnValidate()
	{
		EditorApplication.update -= InternalEditorUtility.RepaintAllViews;
		if (EditorUpdate)
			EditorApplication.update += InternalEditorUtility.RepaintAllViews;
	}

#if UNITY_EDITOR
	void Update()
	{
		if (!EditorUpdate)
			UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
	}
#endif
}