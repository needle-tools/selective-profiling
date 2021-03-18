using System.Collections.Generic;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal interface IContextMenuItemProvider
	{
		void AddItems(Object[] context, int contextUserData, List<ContextItem> items);
	}
}