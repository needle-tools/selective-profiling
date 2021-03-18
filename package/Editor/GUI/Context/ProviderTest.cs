using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal static class ProviderTest
	{
		public class MyProv : IContextMenuItemProvider
		{
			public void AddItems(Object[] context, int contextUserData, List<ContextItem> items)
			{
				items.Add(new ContextItem("Hello/" + context.FirstOrDefault(), false, true, () => Debug.Log("works")));
			}
		}

		[InitializeOnLoadMethod]
		private static void Init()
		{
			ContextMenuPatches.RegisterProvider(new MyProv());
		}
	}
}