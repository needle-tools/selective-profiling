using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using needle.EditorPatching;
using Unity.Profiling.LowLevel;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

namespace Needle.SelectiveProfiling
{
	public static class ProfilerMarkerStore
	{
		[InitializeOnLoadMethod]
		private static void Init()
		{
			ProfilerDriver.NewProfilerFrameRecorded -= OnNewFrame;
			ProfilerDriver.NewProfilerFrameRecorded += OnNewFrame;
		}

		private static readonly List<FrameDataView.MarkerInfo> markers = new List<FrameDataView.MarkerInfo>();

		public static readonly List<(int, string)> captures = new List<(int, string)>();

		private static void OnNewFrame(int thread, int frame)
		{
			// ProfilerDriver.SetMarkerFiltering("Drawing");
			// var raw = ProfilerDriver.GetRawFrameDataView(frame, thread);
			// if (!raw.valid) return;


			using (var frameData = ProfilerDriver.GetRawFrameDataView(frame, 0))
			{
				if (!frameData.valid)
					return;
				var id = frameData.GetMarkerId("MySpecialSample");
				for (var i = 0; i < frameData.sampleCount; i++)
				{
					var markerId = frameData.GetSampleMarkerId(i);
					var name = frameData.GetSampleName(i);
					// var gcAllocSize = frameData.GetSampleMetadataAsFloat(i, 0);
					// if (id != frameData.GetSampleMarkerId(i) || name.ToLowerInvariant().Contains("mouse")) continue;
					if(name.ToLowerInvariant().Contains("mouse") && frameData.HasCounterValue(markerId))
						Debug.Log(i + ": "+ name);
					// captures.Add((frame, name));
				}
			}
			// ProfilerDriver.enabled = false;


			// // ProfilerDriver.SetMarkerFiltering("TEST");
			// markers.Clear();
			// raw.GetMarkers(markers);
			// foreach(var m in markers)
			// 	Debug.Log(m.name);
			// var view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0, HierarchyFrameDataView.ViewModes.Default, 0, false);
			// if (!view.valid)
			// {
			// 	// Debug.Log("View is not valid");
			// 	return;
			// }
			//
			// markers.Clear();
			// // ProfilerDriver.SetMarkerFiltering("TEST");
			// view.GetMarkers(markers);
			// foreach (var m in markers)
			// {
			// 	if (m.name.ToLower().Contains("mouse"))
			// 	{
			// 		Debug.Log(m.name);
			// 	}
			// }

			// ProfilerDriver.SetMarkerFiltering(null);
		}
	}


	public class ProfilerChart_Patch : EditorPatchProvider
	{
		// Profiler Window create chart https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerWindow.cs#L352
		protected override void OnGetPatches(List<EditorPatch> patches)
		{
			patches.Add(new Patch());
		}

		private class Patch : EditorPatch
		{
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.Chart");
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
				// if (___m_Area != ProfilerArea.CPU) return;
				// GUI.Label(rect, "TEST " + r + ", " + selectedFrame);

				// DrawMarker(r, "Frame: " + selectedFrame, selectedFrame, cdata, Color.cyan);
				// var other = cdata.firstSelectableFrame + 20;
				// DrawMarker(r, "Frame: " + other, other, cdata, Color.yellow);

				for (var index = ProfilerMarkerStore.captures.Count - 1; index >= 0; index--)
				{
					var cap = ProfilerMarkerStore.captures[index];
					if (cap.Item1 < cdata.firstSelectableFrame)
					{
						ProfilerMarkerStore.captures.RemoveAt(index);
						continue;
					}
					DrawMarker(r, cap.Item2 + ": " + cap.Item1, cap.Item1, cdata, Color.green);
				}
			}

			// click on marker https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerWindow.cs#L1235
			private static void DrawMarker(Rect r, string label, int frame, ChartViewData cdata, Color color)
			{
				var rect = r;
				rect.height = 0;
				rect.y += r.height;
				var top = DrawVerticalLine(frame, cdata, rect, color, 1);
				rect.x = top.x + 5;
				rect.height = EditorGUIUtility.singleLineHeight;
				rect.y = top.y - rect.height * .5f;
				var prev = GUI.color;
				GUI.color = color;
				GUI.Label(rect, label);
				GUI.color = prev;
			}

			private static Vector2 DrawVerticalLine(int frame, ChartViewData cdata, Rect r, Color color, float minWidth, float maxWidth = 0)
			{
				// if (Event.current.type != EventType.Repaint)
				// 	return;

				frame -= cdata.chartDomainOffset;
				if (frame < 0)
					return Vector2.zero;


				// float domainSize = cdata.GetDataDomainLength();
				float lineWidth = minWidth;
				var count = r.width / cdata.series[0].numDataPoints;
				var x = r.x + frame * count; // r.x + r.width / domainSize * frame;
				var bottom = r.y + 1;
				var top = r.yMax * .5f;
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

			internal class ChartSeriesViewData
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

			internal class ChartViewData
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
		}
	}
}