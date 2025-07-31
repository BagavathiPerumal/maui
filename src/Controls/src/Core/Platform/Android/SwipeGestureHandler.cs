#nullable disable
using System;
using System.Linq;
using Microsoft.Maui.Controls.Internals;

namespace Microsoft.Maui.Controls.Platform
{
	internal class SwipeGestureHandler
	{
		// Threshold below which rotation is considered negligible and transformation is skipped
		const double RotationThreshold = 0.01;

		double _totalX, _totalY;

		Func<double, double> PixelTranslation
		{
			get
			{
				return (input) =>
				{
					var context = GetView()?.Handler?.MauiContext?.Context;
					if (context == null)
						return 0;

					return context.FromPixels(input);
				};
			}
		}

		public SwipeGestureHandler(Func<View> getView)
		{
			GetView = getView;
		}

		Func<View> GetView { get; }

		public bool OnSwipe(float x, float y)
		{
			View view = GetView();

			if (view == null)
				return false;

			var transformedCoords = TransformSwipeCoordinatesWithRotation(x, y, view.Rotation);

			_totalX += PixelTranslation(transformedCoords.x);
			_totalY += PixelTranslation(transformedCoords.y);

			var result = false;
			foreach (SwipeGestureRecognizer swipeGesture in
					view.GestureRecognizers.GetGesturesFor<SwipeGestureRecognizer>())
			{
				((ISwipeGestureController)swipeGesture).SendSwipe(view, _totalX, _totalY);
				result = true;
			}

			return result;
		}

		(float x, float y) TransformSwipeCoordinatesWithRotation(float x, float y, double rotation)
		{
			// Validate input coordinates for NaN or Infinity
			if (!GestureExtensions.AreCoordinatesValid(x, y))
			{
				return (0f, 0f);
			}

			// Validate rotation for NaN or Infinity
			if (!rotation.IsRotationValid())
			{
				return (x, y);
			}

			// Skip transformation for negligible rotation values to avoid unnecessary computation
			if (Math.Abs(rotation) < RotationThreshold)
			{
				return (x, y);
			}

			var normalizedRotation = rotation.NormalizeRotation();

			var radians = normalizedRotation * Math.PI / 180.0;
			var cos = Math.Cos(radians);
			var sin = Math.Sin(radians);

			var transformedX = (float)(x * cos - y * sin);
			var transformedY = (float)(x * sin + y * cos);

			// Validate transformed coordinates for NaN or Infinity
			if (!(transformedX, transformedY).AreCoordinatesValid())
			{
				return (x, y);
			}

			return (transformedX, transformedY);
		}

		public bool OnSwipeComplete()
		{
			View view = GetView();

			if (view == null)
				return false;

			var detected = false;
			foreach (SwipeGestureRecognizer swipeGesture in view.GestureRecognizers.GetGesturesFor<SwipeGestureRecognizer>())
			{
				var gestureDetected = ((ISwipeGestureController)swipeGesture).DetectSwipe(view, swipeGesture.Direction);
				if (gestureDetected)
				{
					detected = true;
				}
			}
			_totalX = 0;
			_totalY = 0;

			return detected;
		}

		public bool HasAnyGestures()
		{
			var view = GetView();
			return view != null && view.GestureRecognizers.OfType<SwipeGestureRecognizer>().Any();
		}
	}
}