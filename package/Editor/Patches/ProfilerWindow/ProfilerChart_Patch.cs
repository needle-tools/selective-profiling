using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using needle.EditorPatching;
using Needle.SelectiveProfiling.CodeWrapper;
using UnityEditor;
using UnityEditor.Graphs;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace Needle.SelectiveProfiling
{
	public struct SelectiveMarker
	{
		[NotNull] public string label;
		[NotNull] public MethodBase method;
	}
	
	public static class ChartMarker
	{
		[InitializeOnLoadMethod]
		private static void Init()
		{
			Add(new SelectiveMarker(){label = "Click", method = AccessTools.Method(typeof(ExecuteEvents), "Execute", new[] {typeof(IPointerClickHandler), typeof(BaseEventData)})});
		}

		public static void Add(string label, MethodBase method)
		{
			Add(new SelectiveMarker()
			{
				label = label,
				method = method
			});
		}

		public static void Add(SelectiveMarker marker)
		{
			if (string.IsNullOrEmpty(marker.label)) throw new Exception("Missing label");
			if (marker.method == null) throw new Exception("Missing methods");
			Debug.Log("Register " + marker.label + ", " + marker.method);
			var prov = new ChartMarkerProvider(marker.label + "@" + marker.method.Name);
			var patch = new ChartMarkerProvider.AddProfilerMarker(marker.label, marker.method);
			prov.Patches.Add(patch);
			PatchManager.RegisterPatch(prov);
			prov.EnablePatch();
		}
	}
	
	[NoAutoDiscover]
	internal class ChartMarkerProvider : EditorPatchProvider
	{
		internal List<AddProfilerMarker> Patches = new List<AddProfilerMarker>();

		private readonly string id;
		public override string ID() => id;

		public ChartMarkerProvider(string id)
		{
			this.id = id;
		}

		public override bool OnWillEnablePatch()
		{
			if (Patches.Count <= 0) return false;
			return base.OnWillEnablePatch();
		}

		protected override void OnGetPatches(List<EditorPatch> patches)
		{
			patches.AddRange(Patches);
		}

		public class AddProfilerMarker : EditorPatch
		{
			private static readonly Dictionary<MethodBase, string> Labels = new Dictionary<MethodBase, string>();

			private readonly string label;
			private readonly MethodBase method;
			[CanBeNull] public IEnumerable<MethodBase> additional;

			public AddProfilerMarker(string label, MethodBase method)
			{
				this.label = label;
				System.Diagnostics.Debug.Assert(method != null, nameof(this.method) + " != null");
				this.method = method;
			}

			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				void Add(MethodBase _method)
				{
					targetMethods.Add(_method);
					if (Labels.ContainsKey(method))
					{
						var existing = Labels[method];
						if (existing != label)
						{
							Debug.Log("Label is already registered " + Labels[method] + ", will override with " + label);
							Labels[method] = label;
						}
					}
					else
						Labels.Add(_method, label);
				}

				Add(method);
				if (additional != null)
				{
					foreach (var ad in additional)
					{
						if (ad == method) continue;
						Add(ad);
					}
				}

				return Task.CompletedTask;
			}

			// ReSharper disable once UnusedMember.Local
			private static IEnumerable<CodeInstruction> Transpiler(MethodBase method, IEnumerable<CodeInstruction> inst)
			{
				var marker = Labels[method];
				ProfilerMarkerStore.AddExpectedMarker(marker);

				void Log(object msg)
				{
					if (SelectiveProfiler.DebugLog == false) return;
					if(msg != null)
						Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, msg.ToString());
				}
				
				Log("-----------------------------");
				Log("Patch " + marker + " in " + method);

				CodeInstruction Emit(CodeInstruction i)
				{
					Log(i);
					return i;
				}

				yield return Emit(new CodeInstruction(OpCodes.Ldstr, marker));
				yield return Emit(CodeInstruction.Call(typeof(Profiler), nameof(Profiler.BeginSample), new[] {typeof(string)}));
				
				// var end = CodeInstruction.Call(typeof(Profiler), nameof(Profiler.EndSample));
				// yield return Emit(end);
				
				foreach (var i in inst)
				{
					if (i.opcode == OpCodes.Ret || i.opcode == OpCodes.Throw)
					{
						var end = CodeInstruction.Call(typeof(Profiler), nameof(Profiler.EndSample));
						i.MoveLabelsTo(end);
						yield return Emit(end);
					}
					
					yield return Emit(i);
				}
				Log("-----------------------------");
			}
		}
	}

	internal struct ChartMarkerData
	{
		public int frame;
		public string label;
		public string tooltip;
		public int chartMarkerId;
		public float millis;

		public ChartMarkerData(int frame, string label, string tooltip, int chartMarkerId, float millis)
		{
			this.frame = frame;
			this.label = label;
			this.tooltip = tooltip;
			this.chartMarkerId = chartMarkerId;
			this.millis = millis;
		}
	}

	internal static class ProfilerMarkerStore
	{
		[InitializeOnLoadMethod]
		private static void Init()
		{
			ProfilerDriver.NewProfilerFrameRecorded -= OnNewFrame;
			ProfilerDriver.NewProfilerFrameRecorded += OnNewFrame;
		}

		private static readonly List<FrameDataView.MarkerInfo> markers = new List<FrameDataView.MarkerInfo>();

		private static readonly HashSet<string> expectedMarkers = new HashSet<string>();
		private static readonly HashSet<int> expectedMarkerIds = new HashSet<int>();

		internal static void AddExpectedMarker(string name)
		{
			if (expectedMarkers.Contains(name)) return;
			expectedMarkers.Add(name);
			expectedMarkerIds.Clear();
		}


		internal static IReadOnlyList<ChartMarkerData> Markers => captures;
		
		internal static void RemoveAt(int index)
		{
			if (index < 0 || index >= captures.Count) return;
			var ex = captures[index];
			captures.RemoveAt(index);
			
			for (var i = lanes.Count - 1; i >= 0; i--)
			{
				var lane = lanes[i];
				if (lane.label == ex.label)
				{
					lane.count -= 1;
					lanes[i] = lane;
					if (lane.count <= 0)
					{
						lanes.RemoveAt(i);
						for (; i < lanes.Count; i++)
						{
							var other = lanes[i];
							other.num -= 1;
							lanes[i] = other;
						}
					}
					break;
				}
			}
		}

		internal static int LaneCount => lanes.Count;

		internal static int GetLane(string label)
		{
			foreach(var lane in lanes)
				if (lane.label == label)
					return lane.num;
			return 0;
		}

		private static void AddToLane(string label)
		{
			for (var index = 0; index < lanes.Count; index++)
			{
				var lane = lanes[index];
				if (lane.label == label)
				{
					lane.count += 1;
					lanes[index] = lane;
					return;
				}
			}
			lanes.Add((label, 1, lanes.Count));
		}

		private static readonly List<ChartMarkerData> captures = new List<ChartMarkerData>();
		private static int capturesCounter = 0;
		private static readonly List<(string label, int count, int num)> lanes = new List<(string label, int count, int num)>();
		

		private static void OnNewFrame(int thread, int frame)
		{
			using (var frameData = ProfilerDriver.GetRawFrameDataView(frame, 0))
			{
				if (!frameData.valid)
					return;

				expectedMarkerIds.Clear();
				foreach (var marker in expectedMarkers)
				{
					var id = frameData.GetMarkerId(marker);
					if (FrameDataView.invalidMarkerId == id) continue;
					expectedMarkerIds.Add(id);
				}

				// var names = new string[ProfilerDriver.GetUISystemEventMarkersCount(frame, 1)];
				// var eventMarker = new EventMarker[ProfilerDriver.GetUISystemEventMarkersCount(frame, 1)];
				// ProfilerDriver.GetUISystemEventMarkersBatch(frame, 1, eventMarker, names);
				// Debug.Log(names.Length);

				HierarchyFrameDataView hierarchy = null;

				for (var i = 0; i < frameData.sampleCount; i++)
				{
					var markerId = frameData.GetSampleMarkerId(i);
					if (expectedMarkerIds.Contains(markerId))
					{
						var name = frameData.GetSampleName(i);
						var tooltip = name + ": " + GetAdditionalMarkerInfo(frameData, i, out var ms);
						captures.Add(new ChartMarkerData(frame, name, tooltip, capturesCounter, ms));
						AddToLane(name);
						capturesCounter += 1;
					}
				}
			}
		}
		
		
		private static readonly List<ulong> callstackBuffer = new List<ulong>();

		private static string GetAdditionalMarkerInfo(RawFrameDataView frame, int sampleIndex, out float millies)
		{
			var builder = new StringBuilder();
			millies = frame.GetSampleTimeMs(sampleIndex);
			builder.AppendLine(millies + "ms");
			
			callstackBuffer.Clear();
			frame.GetSampleCallstack(sampleIndex, callstackBuffer);
			if (callstackBuffer.Count > 0)
			{
				builder.AppendLine("Callstack:");
				foreach (var addr in callstackBuffer)
				{
					var mi = frame.ResolveMethodInfo(addr);
					builder.AppendLine(mi.methodName + " at " + mi.sourceFileLine);
				}
			}

			// remove last line break
			if (builder.Length > 2) builder.Remove(builder.Length - 2, 2);
			return builder.ToString();
		}
	}


	public class ProfilerChart_Patch : EditorPatchProvider
	{
		// TODO: TEST ProfilerDriver.GetUISystemEventMarkersBatch

		// Profiler Window create chart https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerWindow.cs#L352
		protected override void OnGetPatches(List<EditorPatch> patches)
		{
			patches.Add(new DrawMarkerLabels());
			patches.Add(new MarkerLabelClick());
		}

		private static readonly Type ProfilerWindowType = typeof(ProfilerDriver).Assembly.GetType("UnityEditor.ProfilerWindow");
		private static readonly List<(Rect rect, ChartMarkerData marker)> GUIMarkerLabels = new List<(Rect rect, ChartMarkerData marker)>();

		public static void StopProfilingAndSetFrame(int frame)
		{
			var window = EditorWindow.GetWindow(ProfilerWindowType);
			if (window)
			{
				window.Repaint();
				ProfilerWindowType.GetMethod("SetCurrentFrame", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] {frame});
				// ProfilerDriver.enabled = false;
				if (Event.current == null)
				{
					return;
				}

				Event.current.Use();
				Debug.Break();
				GUIUtility.ExitGUI();
			}
		}

		private class MarkerLabelClick : EditorPatch
		{
			private static int selectedId = -1;
			public static string selectedLabel;
			
			internal static bool IsSelected(ChartMarkerData other)
			{
				return selectedLabel == other.label;
			}
			
			internal static bool ShowLabel(ChartMarkerData other)
			{
				return selectedId == other.chartMarkerId;
			}
			
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				var t = typeof(ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerChart");
				var m = t.GetMethod("DoChartGUI", (BindingFlags) ~0);
				targetMethods.Add(m);
				return Task.CompletedTask;
			}

			private static void Prefix()
			{
				if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
				{
					foreach (var e in GUIMarkerLabels)
					{
						var rect = e.rect;
						if (rect.Contains(Event.current.mousePosition))
						{
							selectedId = e.marker.chartMarkerId;
							// if ((Event.current.modifiers & EventModifiers.Alt) != 0)
								selectedLabel = e.marker.label;
							StopProfilingAndSetFrame(e.marker.frame);
							break;
						}
						selectedId = -1;
						selectedLabel = null;
					}
				}
			}
		}

		private class DrawMarkerLabels : EditorPatch
		{
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				var t = typeof(ProfilerDriver).Assembly.GetType("UnityEditorInternal.Chart");
				var m = t.GetMethod("DrawChartStacked", (BindingFlags) ~0);
				targetMethods.Add(m);
				return Task.CompletedTask;
			}


			// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerChart.cs#L120
			// base https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/Chart.cs#L185
			// draw chart stacked https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/Chart.cs#L374
			// DrawVerticalLine https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/Chart.cs#L244
			// ChartViewData https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/Chart.cs#L1111


			private static void Postfix(Rect r, int selectedFrame, ChartViewData cdata)
			{
				GUIMarkerLabels.Clear();

				// if (___m_Area != ProfilerArea.CPU) return;
				// GUI.Label(r, "TEST " + r + ", " + selectedFrame);

				// DrawMarker(r, "Frame: " + selectedFrame, selectedFrame, cdata, Color.cyan);
				if (!Application.isPlaying)
				{
					var fr = cdata.firstSelectableFrame + 20;
					var marker = new ChartMarkerData(fr, "Test", "Tooltip", 0, 10);
					DrawMarker(r, marker, false, false, cdata);
					marker.frame += 20;
					DrawMarker(r, marker, true, true, cdata);
				}

				// var other1 = cdata.firstSelectableFrame + 30;
				// DrawMarker(r, "Frame: " + other, other1, cdata, Color.yellow, true);
				// ProfilerDriver.lastFrameIndex
				var maxHeight = 400;

				for (var index = ProfilerMarkerStore.Markers.Count - 1; index >= 0; index--)
				{
					var cap = ProfilerMarkerStore.Markers[index];
					if (cap.frame < cdata.firstSelectableFrame)
					{
						ProfilerMarkerStore.RemoveAt(index);
						continue;
					}

					var rect = r;
					// rect.y += (cap.chartMarkerId % 3) * 100;
					var lane = ProfilerMarkerStore.GetLane(cap.label);
					rect.y = ((float)lane / ProfilerMarkerStore.LaneCount) * maxHeight;
					cap.tooltip += "\nLane: " + lane + " " + ProfilerMarkerStore.LaneCount;
					var showLabel = selectedFrame == cap.frame || MarkerLabelClick.ShowLabel(cap);
					var selected = MarkerLabelClick.IsSelected(cap);
					DrawMarker(rect, cap, selected, showLabel, cdata);
				}
			}

			static class Styles
			{
				public static readonly GUIStyle whiteLabel = new GUIStyle("ProfilerBadge");
			}

			// click on marker https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerWindow.cs#L1235
			private static void DrawMarker(Rect r, ChartMarkerData chartMarker, bool selected, bool showLabel, ChartViewData cdata, bool drawLine = false)
			{
				var color = GUIColors.GetColorOnGradient(GUIColors.NaiveCalculateGradientPosition(chartMarker.millis, 0));
				
				var rect = r;
				rect.height = 0;
				rect.y += r.height;
				rect.yMax *= 0.15f;
				// drawLine = true;
				if (drawLine)
				{
					var top = DrawVerticalLine(chartMarker.frame, cdata, rect, color, 1);
					rect.x = top.x;
					rect.y = top.y;
				}
				else
				{
					rect.x = GetFrameX(chartMarker.frame, cdata, rect);
					rect.y = rect.yMax;
				}

				rect.y -= 2;
				rect.height = EditorGUIUtility.singleLineHeight;

				var prev = GUI.color;
				GUI.color = color;
				var content = new GUIContent(chartMarker.label, chartMarker.tooltip);
				var size = Styles.whiteLabel.CalcSize(content);
				rect.size = size;
				// rect.x -= 5;
				// rect.x -= size.x * .5f;

				void RegisterClickableMarker(Rect clickRect)
				{
					GUIMarkerLabels.Add((clickRect, chartMarker));
				}
				
				var circleSize = selected ? 6 : 4;
				if (showLabel)
				{
					var labelRect = rect;
					labelRect.x += 2;
					labelRect.y -= labelRect.size.y * .5f;
					RegisterClickableMarker(labelRect);
					// Styles.whiteLabel.normal.background = Textures.WhiteLabel;
					// Styles.whiteLabel.normal.textColor = Color.white;
					GUI.color = Color.white;
					EditorGUI.DropShadowLabel(
						labelRect,
						content,
						Styles.whiteLabel
					);
				}

				void DrawCircle(float circleSize)
				{
					var circleRect = new Rect(rect.x, rect.y, circleSize, circleSize);
					var offset = circleSize * .5f;
					circleRect.x -= offset;
					circleRect.y -= offset;
					var clickRect = circleRect;
					const float clickPadding = 10;
					var clickSize = clickPadding + circleSize;
					clickRect.size = Vector2.one * (clickSize);
					offset = (clickSize - circleSize) * .25f;
					clickRect.x -= offset;
					clickRect.y -= offset;
					RegisterClickableMarker(clickRect);
					GUI.DrawTexture(circleRect, Textures.CircleFilled, ScaleMode.ScaleAndCrop, true, 1, GUI.color, 0, 0);
					GUI.Label(rect, new GUIContent(string.Empty, content.tooltip));
				}
				GUI.color = color;
				DrawCircle(circleSize);
				
				
				GUI.color = prev;
			}

			private static float GetFrameX(int frame, ChartViewData cdata, Rect rect)
			{
				frame -= cdata.chartDomainOffset;
				var count = rect.width / cdata.series[0].numDataPoints;
				return rect.x + frame * count;
				;
			}

			private static Vector2 DrawVerticalLine(int frame, ChartViewData cdata, Rect rect, Color color, float minWidth)
			{
				// if (Event.current.type != EventType.Repaint)
				// 	return;

				if (frame < 0)
					return Vector2.zero;


				// float domainSize = cdata.GetDataDomainLength();
				var x = GetFrameX(frame, cdata, rect);
				var lineWidth = minWidth;
				var bottom = rect.y + 1;
				var top = rect.yMax;
				// Debug.Log(x + ", " + domainSize + ", " + r.width + ", " + cdata.firstSelectableFrame + ", " + cdata.series[0].numDataPoints); 

				// HandleUtility.ApplyWireMaterial();
				typeof(HandleUtility).GetMethod("ApplyWireMaterial", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[0], null).Invoke(null, null);

				GL.Begin(GL.QUADS);
				GL.Color(color);
				GL.Vertex3(x, bottom, 0);
				GL.Vertex3(x + lineWidth, bottom, 0);

				GL.Vertex3(x + lineWidth, top, 0);
				GL.Vertex3(x, top, 0);
				GL.End();
				return new Vector2(x, top);
			}

			#region Mock Classes

			private class ChartSeriesViewData
			{
				public string name { get; private set; }
				public Color color { get; private set; }
				public bool enabled;
				public float[] xValues { get; private set; }
				public float yScale { get; internal set; }
				public float[] yValues { get; private set; }
				public Vector2 rangeAxis { get; set; }
				public int numDataPoints { get; private set; }
			}

			private class ChartViewData
			{
				public ChartSeriesViewData[] series { get; private set; }
				public ChartSeriesViewData[] overlays { get; private set; }
				public int[] order { get; private set; }
				public float[] grid { get; private set; }
				public string[] gridLabels { get; private set; }
				public string[] selectedLabels { get; private set; }

				/// <summary>
				/// if dataAvailable has this bit set, there is data
				/// </summary>
				public const int dataAvailableBit = 1;

				/// <summary>
				/// 0 = No Data
				/// bit 1 set = Data available
				/// >1 = There's a additional info that may provide a reason for missing data
				/// </summary>
				public int[] dataAvailable { get; set; }

				public int firstSelectableFrame { get; private set; }
				public bool hasOverlay { get; set; }
				public float maxValue { get; set; }
				public int numSeries { get; private set; }
				public int chartDomainOffset { get; private set; }


				public Vector2 GetDataDomain()
				{
					// TODO: this currently assumes data points are in order and first series has at least one data point
					if (series == null || numSeries == 0 || series[0].numDataPoints == 0)
						return Vector2.zero;
					Vector2 result = Vector2.one * series[0].xValues[0];
					for (int i = 0; i < numSeries; ++i)
					{
						if (series[i].numDataPoints == 0)
							continue;
						result.x = Mathf.Min(result.x, series[i].xValues[0]);
						result.y = Mathf.Max(result.y, series[i].xValues[series[i].numDataPoints - 1]);
					}

					return result;
				}

				public int GetDataDomainLength()
				{
					var domain = GetDataDomain();
					// the domain is a range of indices, logically starting at 0. The Length is therefore the (lastIndex - firstIndex + 1)
					return (int) (domain.y - domain.x) + 1;
				}
			}

			#endregion
		}
	}
}