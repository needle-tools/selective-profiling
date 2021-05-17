using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Needle.SelectiveProfiling
{
	public class ProfilerChart_Patch
	{
		// TODO: TEST ProfilerDriver.GetUISystemEventMarkersBatch
		// Profiler Window create chart https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerWindow.cs#L352

		private static readonly Type ProfilerWindowType = typeof(ProfilerDriver).Assembly.GetType("UnityEditor.ProfilerWindow");
		private static readonly List<(Rect rect, ChartMarkerData marker)> GUIMarkerLabels = new List<(Rect rect, ChartMarkerData marker)>();
		private static MethodInfo setCurrentFrame;

		private static void StopProfilingAndSetFrame(int frame)
		{
			var window = EditorWindow.GetWindow(ProfilerWindowType);
			if (window)
			{
				window.Repaint();
				if (setCurrentFrame == null) setCurrentFrame = ProfilerWindowType.GetMethod("SetCurrentFrame", BindingFlags.Instance | BindingFlags.NonPublic);
				setCurrentFrame?.Invoke(window, new object[] {frame});
				if (Event.current == null) return;
				Event.current.Use();
				Debug.Break();
				GUIUtility.ExitGUI();
			}
		}

		private class MarkerLabelClick : PatchBase
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

			protected override IEnumerable<MethodBase> GetPatches()
			{
				var t = typeof(ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerChart");
				var m = t.GetMethod("DoChartGUI", (BindingFlags) ~0);
				yield return m;
			}

			private static void Prefix()
			{
				if (ProfilerHelper.IsDeepProfiling) return;
				
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
							ProfilerTreeView_Patch.RequestExpandItemId = e.marker.itemId;
							ProfilerTreeView_Patch.RequestExpandMarkerId = e.marker.markerId;
							StopProfilingAndSetFrame(e.marker.frame);
							break;
						}

						selectedId = -1;
						selectedLabel = null;
					}
				}
			}
		}

		private class DrawMarkerLabels : PatchBase
		{
			protected override IEnumerable<MethodBase> GetPatches()
			{
				var t = typeof(ProfilerDriver).Assembly.GetType("UnityEditorInternal.Chart");
				var m = t.GetMethod("DrawChartStacked", (BindingFlags) ~0);
				yield return m;
			}


			// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerChart.cs#L120
			// base https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/Chart.cs#L185
			// draw chart stacked https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/Chart.cs#L374
			// DrawVerticalLine https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/Chart.cs#L244
			// ChartViewData https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/Chart.cs#L1111


			private static void Postfix(Rect r, int selectedFrame, ChartViewData cdata)
			{
				GUIMarkerLabels.Clear();
				
				if (ProfilerHelper.IsDeepProfiling) return;

				// if (___m_Area != ProfilerArea.CPU) return;
				// GUI.Label(r, "TEST " + r + ", " + selectedFrame);

				// DrawMarker(r, "Frame: " + selectedFrame, selectedFrame, cdata, Color.cyan);
				if (!Application.isPlaying && ProfilerMarkerStore.Markers.Count <= 0)
				{
					var fr = cdata.firstSelectableFrame + 20;
					var marker = new ChartMarkerData(fr, 0, "Test", "Tooltip", 0, 10);
					DrawMarker(r, marker, false, false, cdata);
					marker.frame += 20;
					DrawMarker(r, marker, true, true, cdata);
				}

				// var other1 = cdata.firstSelectableFrame + 30;
				// DrawMarker(r, "Frame: " + other, other1, cdata, Color.yellow, true);
				// ProfilerDriver.lastFrameIndex
				const int maxHeight = 400;

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
					rect.y = ((float) lane / ProfilerMarkerStore.LaneCount) * maxHeight;
					cap.tooltip += "\nFound: " + cap.count;
					var showLabel = selectedFrame == cap.frame || MarkerLabelClick.ShowLabel(cap);
					var selected = MarkerLabelClick.IsSelected(cap);
					DrawMarker(rect, cap, selected, showLabel, cdata);
				}
			}

			private static class Styles
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

				void DrawCircle(float csize)
				{
					var circleRect = new Rect(rect.x, rect.y, csize, csize);
					var offset = csize * .5f;
					circleRect.x -= offset;
					circleRect.y -= offset;
					var clickRect = circleRect;
					const float clickPadding = 10;
					var clickSize = clickPadding + csize;
					clickRect.size = Vector2.one * (clickSize);
					offset = (clickSize - csize) * .25f;
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
			}

			private static MethodInfo applyWireMaterial;

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

				if (applyWireMaterial == null)
					applyWireMaterial =
						typeof(HandleUtility).GetMethod("ApplyWireMaterial", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[0], null);
				applyWireMaterial?.Invoke(null, null);

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