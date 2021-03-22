using System.Collections.Generic;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public interface IContextMenuItemProvider
	{
		void AddItems(Object[] context, int contextUserData, List<ContextItem> items);
	}
}