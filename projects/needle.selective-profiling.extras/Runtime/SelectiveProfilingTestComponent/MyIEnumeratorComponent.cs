using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MyIEnumeratorComponent : MonoBehaviour, IEnumerator<int>
{
	public Func<bool> Callback;

	private void Start()
	{
		Callback = () => true;
	}

	private void Update()
	{
		MoveNext();
	}

	public bool MoveNext()
	{
		if (HasAnyHealthCondition(Time.frameCount % 2 == 0))
			Test();
		return Callback?.Invoke() ?? false;
	}

	public void Reset()
	{
	}

	public int Current { get; }

	object IEnumerator.Current => Current;

	public void Dispose()
	{
	}

	private void Test()
	{
	}

	private bool HasAnyHealthCondition(bool t)
	{
		return t;
	}
}