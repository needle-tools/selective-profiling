using System;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public static class ExceptionExtensions
	{
		public static bool IsOrHasUnityException_CanOnlyBeCalledFromMainThread(this Exception e)
		{
			if (e == null) return false;
			if (e is UnityException un && un.Message.Contains("can only be called from the main thread."))
				return true;
			if (e.InnerException != null)
				return IsOrHasUnityException_CanOnlyBeCalledFromMainThread(e.InnerException);
			return false;
		}
	}
}