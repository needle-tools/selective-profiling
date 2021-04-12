namespace Needle.SelectiveProfiling
{
	public class PerformanceData
	{
		public int InstanceId;
		internal bool isValid;
		public float TotalMs;
		public float Alloc;

		internal void Clear()
		{
			TotalMs = 0;
			Alloc = 0;
		}

		internal void Add(PerformanceData other)
		{
			if (!other.isValid) return;
			TotalMs += other.TotalMs;
			Alloc += other.Alloc;
		}
	}
}