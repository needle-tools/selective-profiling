#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[ExecuteInEditMode]
public class RepaintEverythingEveryFrame : MonoBehaviour
{

	private void OnValidate()
	{
		EditorApplication.update -= OnRepaintNow;
		if (enabled)
			EditorApplication.update += OnRepaintNow;
	}

	private void OnEnable()
	{
		EditorApplication.update -= OnRepaintNow;
		EditorApplication.update += OnRepaintNow;
	}

	private void OnDisable()
	{
		EditorApplication.update -= OnRepaintNow;
	}

	private void OnRepaintNow()
	{
		if (ProfilerDriver.enabled)
		{
			InternalEditorUtility.RepaintAllViews();
		}
	}
}

#endif