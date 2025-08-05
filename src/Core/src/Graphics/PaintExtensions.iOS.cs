using System;
using System.Collections.Generic;
using System.Linq;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Maui.Graphics
{
	internal class RadialGradientCALayer : CALayer, IAutoSizableCALayer
	{
		private readonly RadialGradientPaint _radialGradientPaint;
		
		[UnconditionalSuppressMessage("Memory", "MEM0002", Justification = "Proven safe in CALayerAutosizeObserver_DoesNotLeak test.")]
		CALayerAutosizeObserver? _boundsObserver;

		public RadialGradientCALayer(RadialGradientPaint radialGradientPaint)
		{
			_radialGradientPaint = radialGradientPaint;
			SetNeedsDisplay();
		}

		protected override void Dispose(bool disposing)
		{
			_boundsObserver?.Dispose();
			_boundsObserver = null;
			base.Dispose(disposing);
		}

		public override void RemoveFromSuperLayer()
		{
			_boundsObserver?.Dispose();
			_boundsObserver = null;
			base.RemoveFromSuperLayer();
		}

		void IAutoSizableCALayer.AutoSizeToSuperLayer()
		{
			_boundsObserver?.Dispose();
			_boundsObserver = CALayerAutosizeObserver.Attach(this);
		}

		public override void DrawInContext(CGContext ctx)
		{
			if (_radialGradientPaint?.GradientStops == null || _radialGradientPaint.GradientStops.Length == 0)
				return;

			var bounds = Bounds;
			if (bounds.Width <= 0 || bounds.Height <= 0)
				return;

			var center = _radialGradientPaint.Center;
			var radius = _radialGradientPaint.Radius;

			// Create gradient colors and locations
			var orderedStops = _radialGradientPaint.GradientStops.OrderBy(x => x.Offset).ToArray();
			var gradientColors = new nfloat[orderedStops.Length * 4];
			var locations = new nfloat[orderedStops.Length];

			int colorIndex = 0;
			for (int i = 0; i < orderedStops.Length; i++)
			{
				var color = orderedStops[i].Color;
				locations[i] = (nfloat)orderedStops[i].Offset;

				gradientColors[colorIndex++] = (nfloat)color.Red;
				gradientColors[colorIndex++] = (nfloat)color.Green;
				gradientColors[colorIndex++] = (nfloat)color.Blue;
				gradientColors[colorIndex++] = (nfloat)color.Alpha;
			}

			// Create the gradient
			using var colorSpace = CGColorSpace.CreateDeviceRGB();
			using var gradient = new CGGradient(colorSpace, gradientColors, locations);

			// Calculate the actual center and radius in view coordinates
			var centerPoint = new CGPoint(
				center.X * bounds.Width + bounds.X,
				center.Y * bounds.Height + bounds.Y
			);

			var actualRadius = (nfloat)(radius * Math.Max(bounds.Width, bounds.Height));

			// Draw the radial gradient
			ctx.DrawRadialGradient(
				gradient,
				centerPoint, 0.0f,        // Start point with radius 0
				centerPoint, actualRadius, // End point with calculated radius
				CGGradientDrawingOptions.DrawsBeforeStartLocation | CGGradientDrawingOptions.DrawsAfterEndLocation
			);
		}
	}

	public static partial class PaintExtensions
	{
		public static CALayer? ToCALayer(this Paint paint, CGRect frame = default)
		{
			if (paint is SolidPaint solidPaint)
				return solidPaint.CreateCALayer(frame);

			if (paint is LinearGradientPaint linearGradientPaint)
				return linearGradientPaint.CreateCALayer(frame);

			if (paint is RadialGradientPaint radialGradientPaint)
				return radialGradientPaint.CreateCALayer(frame);

			if (paint is ImagePaint imagePaint)
				return imagePaint.CreateCALayer(frame);

			if (paint is PatternPaint patternPaint)
				return patternPaint.CreateCALayer(frame);

			return null;
		}

		public static CALayer? CreateCALayer(this SolidPaint solidPaint, CGRect frame = default)
		{
			var solidColorLayer = new StaticCALayer
			{
				ContentsGravity = CALayer.GravityTop,
				Frame = frame,
				BackgroundColor = solidPaint.Color.ToCGColor()
			};

			return solidColorLayer;
		}

		public static CALayer? CreateCALayer(this GradientPaint gradientPaint, CGRect frame = default)
		{
			if (gradientPaint is LinearGradientPaint linearGradientPaint)
				return linearGradientPaint.CreateCALayer(frame);

			if (gradientPaint is RadialGradientPaint radialGradientPaint)
				return radialGradientPaint.CreateCALayer(frame);

			return null;
		}

		public static CALayer? CreateCALayer(this LinearGradientPaint linearGradientPaint, CGRect frame = default)
		{
			var p1 = linearGradientPaint.StartPoint;
			var p2 = linearGradientPaint.EndPoint;

			var linearGradientLayer = new StaticCAGradientLayer
			{
				ContentsGravity = CALayer.GravityResizeAspectFill,
				Frame = frame,
				LayerType = CAGradientLayerType.Axial,
				StartPoint = new CGPoint(p1.X, p1.Y),
				EndPoint = new CGPoint(p2.X, p2.Y)
			};

			if (linearGradientPaint.GradientStops != null && linearGradientPaint.GradientStops.Length > 0)
			{
				var orderedStops = linearGradientPaint.GradientStops.OrderBy(x => x.Offset).ToList();
				linearGradientLayer.Colors = GetCAGradientLayerColors(orderedStops);
				linearGradientLayer.Locations = GetCAGradientLayerLocations(orderedStops);
			}

			return linearGradientLayer;
		}

		public static CALayer? CreateCALayer(this RadialGradientPaint radialGradientPaint, CGRect frame = default)
		{
			var radialGradientLayer = new RadialGradientCALayer(radialGradientPaint)
			{
				ContentsGravity = CALayer.GravityResizeAspectFill,
				Frame = frame
			};

			return radialGradientLayer;
		}

		public static CALayer? CreateCALayer(this ImagePaint imagePaint, CGRect frame = default)
		{
			throw new NotImplementedException();
		}

		public static CALayer? CreateCALayer(this PatternPaint patternPaint, CGRect frame = default)
		{
			throw new NotImplementedException();
		}

		static NSNumber[] GetCAGradientLayerLocations(List<PaintGradientStop> gradientStops)
		{
			if (gradientStops == null || gradientStops.Count == 0)
				return Array.Empty<NSNumber>();

			if (gradientStops.Count > 1 && gradientStops.Any(gt => gt.Offset != 0))
				return gradientStops.Select(x => new NSNumber(x.Offset)).ToArray();
			else
			{
				int itemCount = gradientStops.Count;
				int index = 0;
				float step = 1.0f / itemCount;

				NSNumber[] locations = new NSNumber[itemCount];

				foreach (var gradientStop in gradientStops)
				{
					float location = step * index;
					bool setLocation = !gradientStops.Any(gt => gt.Offset > location);

					if (gradientStop.Offset == 0 && setLocation)
						locations[index] = new NSNumber(location);
					else
						locations[index] = new NSNumber(gradientStop.Offset);

					index++;
				}

				return locations;
			}
		}

		static CGColor[] GetCAGradientLayerColors(List<PaintGradientStop> gradientStops)
		{
			if (gradientStops == null || gradientStops.Count == 0)
				return Array.Empty<CGColor>();

			CGColor[] colors = new CGColor[gradientStops.Count];

			int index = 0;
			foreach (var gradientStop in gradientStops)
			{
				if (gradientStop.Color == Colors.Transparent)
				{
					var color = gradientStops[index == 0 ? index + 1 : index - 1].Color;
					CGColor nativeColor = color.ToPlatform().ColorWithAlpha(0.0f).CGColor;
					colors[index] = nativeColor;
				}
				else
					colors[index] = gradientStop.Color.ToCGColor();

				index++;
			}

			return colors;
		}
	}
}