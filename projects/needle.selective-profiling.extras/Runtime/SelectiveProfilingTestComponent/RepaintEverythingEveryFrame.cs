using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[ExecuteInEditMode]
public class RepaintEverythingEveryFrame : MonoBehaviour
{

	private void OnValidate()
	{
		EditorApplication.update -= OnUpdate;
		if (enabled)
			EditorApplication.update += OnUpdate;
	}

	private void OnEnable()
	{
		EditorApplication.update -= OnUpdate;
		EditorApplication.update += OnUpdate;
	}

	private void OnDisable()
	{
		EditorApplication.update -= OnUpdate;
	}

	private void OnUpdate()
	{
		if (ProfilerDriver.enabled)
		{
			InternalEditorUtility.RepaintAllViews();
		}
	}
}