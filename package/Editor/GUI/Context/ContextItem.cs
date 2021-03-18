using System;

namespace Needle.SelectiveProfiling
{
	internal class ContextItem
	{
		public string Path;
		public bool Separator;
		public bool Enabled;
		public Action Selected;

		public ContextItem(string path, bool separator, bool enabled, Action selected)
		{
			Path = path;
			Separator = separator;
			Enabled = enabled;
			Selected = selected;
		}
	}
}