// using System.Reflection;
// using UnityEngine;
//
// namespace Needle.SelectiveProfiling
// {
// 	public interface IProfileRestriction
// 	{
// 		abstract bool AllowProfiling(MethodInfo info);
// 		bool AllowDeepProfiling(MethodInfo info);
// 	}
// 	
// 	public abstract class ProfileRestrictionRule : ScriptableObject, IProfileRestriction
// 	{
// 		public abstract bool AllowProfiling(MethodInfo info);
// 		public virtual bool AllowDeepProfiling(MethodInfo info) => true;
// 	}
// }