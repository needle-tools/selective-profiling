using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public static class ConsoleHyperlink
	{
		/// <summary>
		/// return true if callback is handled and should stop iteration
		/// </summary>
		public delegate bool ClickedCallback(string path, string line);

		/// <summary>
		/// register hyperlink clicked handler
		/// </summary>
		/// <param name="callback">callback invoked when hyperlink clicked</param>
		/// <param name="priority">higher priority is called first</param>
		public static void RegisterClickedCallback(ClickedCallback callback, int priority)
		{
			registered.Add((priority, callback));
			dirty = true;
		}

		private static List<(int priority, ClickedCallback callback)> registered = new List<(int priority, ClickedCallback callback)>();
		private static bool dirty;

		private static void EnsureCallbacksOrdered()
		{
			if (!dirty) return;
			registered = registered.OrderByDescending(p => p.priority).ToList();
			dirty = false;
		}

		[InitializeOnLoadMethod]
		private static void Init()
		{
			// Debug.Log("My Log) (at www.google.de:0)");
			// Debug.Log("My Log) (at https://google.de:0)");
			// Debug.Log("My Log) (at https://www.google.de:0)");
			// Debug.Log("My Log) (at http://www.google.de:0)");
			// Debug.Log("My Log) (at C:/git/needle-packages-master/development/debughelpers/README.md)");
			
			var evt = typeof(EditorGUI).GetEvent("hyperLinkClicked", BindingFlags.Static | BindingFlags.NonPublic);
			if (evt != null)
			{
				var method = typeof(ConsoleHyperlink).GetMethod("OnClicked", BindingFlags.Static | BindingFlags.NonPublic);
				if (method != null)
				{
					var handler = Delegate.CreateDelegate(evt.EventHandlerType, method);
					evt.AddMethod.Invoke(null, new object[] {handler});
				}
			}
		}

		private static PropertyInfo property;

		// ReSharper disable once UnusedMember.Local
		private static void OnClicked(object sender, EventArgs args)
		{
			if (property == null)
			{
				property = args.GetType().GetProperty("hyperlinkInfos", BindingFlags.Instance | BindingFlags.Public);
				if (property == null) return;
			}

			if (property.GetValue(args) is Dictionary<string, string> infos)
			{
				if (infos.TryGetValue("href", out var path))
				{
					infos.TryGetValue("line", out var line);
					EnsureCallbacksOrdered();
					foreach (var cb in registered)
					{
						var res = cb.callback?.Invoke(path, line) ?? false;
						if (res) break;
					}
				}
			}
		}

		[InitializeOnLoadMethod]
		private static void URLCallback()
		{
			RegisterClickedCallback((path, line) =>
			{
				if (path.StartsWith("www."))
				{
					// Debug.Log("Open " + path);
					Application.OpenURL(path);
				}
				
				var result = Uri.TryCreate(path, UriKind.Absolute, out var uriResult) 
				             && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
				
				if (result)
				{
					var url = uriResult.ToString();
					// Debug.Log("Open " + url);
					Application.OpenURL(url);
				}

				return result;
			}, 0);
		}
	}
}