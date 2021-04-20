using System;
using System.Threading.Tasks;

namespace Needle.SelectiveProfiling.Commands
{
	public class ActionCommand : Command
	{
		private readonly Action action;

		public Task GetTask() => new Task(action);
		
		public ActionCommand(Action action)
		{
			this.action = action;
		}
		
		protected override void Execute()
		{
			action?.Invoke();
		}
	}
}