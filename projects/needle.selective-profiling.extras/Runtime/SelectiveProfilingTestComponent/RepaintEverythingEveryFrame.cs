using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[ExecuteInEditMode]
public class RepaintEverythingEveryFrame : MonoBehaviour
{

	private void OnValidate()
	{
		EditorApplication.update -= InternalEditorUtility.RepaintAllViews;
		if (enabled)
			EditorApplication.update += InternalEditorUtility.RepaintAllViews;
	}

	private void OnEnable()
	{
		EditorApplication.update -= InternalEditorUtility.RepaintAllViews;
		EditorApplication.update += InternalEditorUtility.RepaintAllViews;
	}

	private void OnDisable()
	{
		EditorApplication.update -= InternalEditorUtility.RepaintAllViews;
	}
}