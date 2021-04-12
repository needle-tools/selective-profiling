namespace Needle.SelectiveProfiling
{
	public class PerformanceData
	{
		public int InstanceId;
		public float TotalMs;
		internal bool isValid;

		internal void Clear()
		{
			TotalMs = 0;
		}

		internal void Add(PerformanceData other)
		{
			if (!other.isValid) return;
			TotalMs += other.TotalMs;
		}
	}
}