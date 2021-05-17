using HarmonyLib;

namespace Needle.SelectiveProfiling
{
	public interface IPatch
	{
		void Apply(Harmony harmony);
		void Remove(Harmony harmony);
		string Id { get; }
		string DisplayName { get; }
		bool SuppressUnityExceptions { get; set; }
		bool PatchThreaded { get; set; }
	}
}