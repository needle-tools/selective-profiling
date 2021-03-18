using System;

namespace Needle.SelectiveProfiling
{
	internal class ContextItem
	{
		public string Path;
		public Action Selected;
		public bool Separator = false;
		public bool Enabled = true;

		public ContextItem(string path, Action selected)
		{
			Path = path;
			Selected = selected;
		}
	}
}