#if UNITY_EDITOR


using System;
using UnityEditor;
using UnityEngine;

namespace SelectiveProfilingTestComponent
{
	public class SomeEditorWindow : EditorWindow
	{
		private void OnGUI()
		{
			SomethingWithArgs(5);
			
			// bool test = true;
			bool selected = false;
			string label = "test";
			// bool myval = test;

			if (IsRenaming())
			{
				selected = true;
				label = "";
			}

			if (Event.current.type == EventType.Repaint || IsRenaming("calling a method"))
			{
				Debug.Log("is repaint " + label);
			}
			// else Debug.Log(Event.current);

			string marker = "test 2 with false";
			if (!IsRenaming())
			{
				selected = true;
				label = "";
			}

			string str = "loop0";
			for (int i = 0; i <= 1; i++)
			{
				if (str == "loop1") Debug.Log("HELLO " + i);
				Debug.Log("run: " + str);
				str = "loop1";
			}
			
			return;
			
			Debug.Log("is never called " + label + ", " + selected);

			for (int i = 0; i < 5; i++)
			{
				Debug.Log(i);
			}
		}

		private bool SomethingWithArgs(int i)
		{
			i += 2;
			return true;
		}

		[MenuItem("Test/EditorWindowWithRepaint")]
		private static void Test()
		{
			CreateInstance<SomeEditorWindow>().Show();
		}

		private bool IsRenaming() => false;
		private bool IsRenaming(string test) => false;
	}
}

#endif