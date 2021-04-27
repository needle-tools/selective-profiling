using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Needle.SelectiveProfiling
{
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
				var rect = r;
				rect.height = EditorGUIUtility.singleLineHeight;
				rect.y += r.height - rect.height;
				GUI.Label(rect, "TEST " + r + ", " + selectedFrame);

				DrawVerticalLine(selectedFrame , cdata, r, Color.red, 1);
			}


			internal static void DrawVerticalLine(int frame, ChartViewData cdata, Rect r, Color color, float minWidth, float maxWidth = 0)
			{
				// if (Event.current.type != EventType.Repaint)
				// 	return;

				frame -= cdata.chartDomainOffset;
				if (frame < 0)
					return;


				float domainSize = cdata.GetDataDomainLength();
				float lineWidth = minWidth;
				var x = r.x + frame;// r.x + r.width / domainSize * frame;
				var count = r.width / cdata.series[0].numDataPoints;

				// HandleUtility.ApplyWireMaterial();
				typeof(HandleUtility).GetMethod("ApplyWireMaterial", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[0], null).Invoke(null, null);
				GL.Begin(GL.QUADS);
				GL.Color(color);
				Debug.Log(x + ", " + domainSize + ", " + r.width + ", " + cdata.firstSelectableFrame + ", " + cdata.series[0].numDataPoints); 
				GL.Vertex3(x, r.y + 1, 0);
				GL.Vertex3(x + lineWidth, r.y + 1, 0);

				GL.Vertex3(x + lineWidth, r.yMax, 0);
				GL.Vertex3(x, r.yMax, 0);
				GL.End();
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
					return (int)(domain.y - domain.x) + 1;
				}
			}
		}
	}
}