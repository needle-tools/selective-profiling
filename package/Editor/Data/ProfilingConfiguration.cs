// using System;
// using System.Collections.Generic;
// using UnityEngine;
//
// namespace Needle.SelectiveProfiling
// {
// 	[Serializable]
// 	internal class ProfilingConfiguration
// 	{
// 		[SerializeField]
// 		private string Id;
// 		public string Name;
// 		public List<MethodInformation> Methods = new List<MethodInformation>();
// 		public List<MethodInformation> Muted = new List<MethodInformation>();
//
// 		public ProfilingConfiguration(string name)
// 		{
// 			this.Name = name;
// 			this.Id = Guid.NewGuid().ToString();
// 		}
// 	}
// }