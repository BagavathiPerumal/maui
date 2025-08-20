using System;
using CoreAnimation;
using CoreGraphics;
using Microsoft.Maui.Graphics;
using ObjCRuntime;
using UIKit;

namespace Microsoft.Maui.Platform
{
	public static class TransformationExtensions
	{
		const float ZPositionSpacing = 100.0f;
		const float ZPositionBaseOffset = 50.0f;
		const float ZPositionSiblingOffset = 2000.0f;

		public static void UpdateTransformation(this UIView platformView, IView? view)
		{
			CALayer? layer = platformView.Layer;
			CGPoint? originalAnchor = layer?.AnchorPoint;

			platformView.UpdateTransformation(view, layer, originalAnchor);
		}

		public static void UpdateTransformation(this UIView platformView, IView? view, CALayer? layer, CGPoint? originalAnchor)
		{
			if (view == null)
				return;

			var anchorX = (float)view.AnchorX;
			var anchorY = (float)view.AnchorY;
			var translationX = (float)view.TranslationX;
			var translationY = (float)view.TranslationY;
			var rotationX = (float)view.RotationX;
			var rotationY = (float)view.RotationY;
			var rotation = (float)view.Rotation;
			var scale = (float)view.Scale;
			var scaleX = (float)view.ScaleX * scale;
			var scaleY = (float)view.ScaleY * scale;
			var width = (float)view.Frame.Width;
			var height = (float)view.Frame.Height;
			var x = (float)view.Frame.X;
			var y = (float)view.Frame.Y;

			void Update()
			{
				var shouldUpdate =
					width > 0 &&
					height > 0 &&
					view.Parent != null;

				if (!shouldUpdate)
					return;

				const double epsilon = 0.001;

				var transform = CATransform3D.Identity;

				var has3DRotation = Math.Abs(rotationY) > epsilon || Math.Abs(rotationX) > epsilon;

				if (layer is not null && has3DRotation)
				{
					var superview = platformView.Superview;
					var hasSiblings = superview?.Subviews?.Length > 1;
						
					if (hasSiblings && superview != null)
					{
						var subviews = superview.Subviews;
						var currentIndex = Array.IndexOf(subviews, platformView);
												
						if (currentIndex >= 0)
						{
							layer.ZPosition = currentIndex * ZPositionSpacing + ZPositionBaseOffset;
							
							for (int i = 0; i < subviews.Length; i++)
							{
								var siblingView = subviews[i];
								if (siblingView != platformView && siblingView.Layer != null)
								{
									var siblingLayer = siblingView.Layer;
									
									if (i > currentIndex)
									{
										var expectedZPosition = (currentIndex * ZPositionSpacing) + ZPositionSiblingOffset + (i * ZPositionSpacing);
										siblingLayer.ZPosition = expectedZPosition;
									}
									else
									{
										var expectedZPosition = i * ZPositionSpacing;
										siblingLayer.ZPosition = expectedZPosition;
									}
								}
							}
						}
					}
				}
				else if (layer is not null && !has3DRotation && HasModifiedZPositions(platformView))
				{
					RestoreNaturalZOrder(layer, platformView);
				}

				// Position is relative to anchor point
				if (Math.Abs(anchorX - .5) > epsilon)
					transform = transform.Translate((anchorX - .5f) * width, 0, 0);

				if (Math.Abs(anchorY - .5) > epsilon)
					transform = transform.Translate(0, (anchorY - .5f) * height, 0);

				if (Math.Abs(translationX) > epsilon || Math.Abs(translationY) > epsilon)
					transform = transform.Translate(translationX, translationY, 0);

				// Not just an optimization, iOS will not "pixel align" a view which has M34 set
				if (Math.Abs(rotationY % 180) > epsilon || Math.Abs(rotationX % 180) > epsilon)
					transform.M34 = 1.0f / -400f;

				if (Math.Abs(rotationX % 360) > epsilon)
					transform = transform.Rotate(rotationX * MathF.PI / 180.0f, 1.0f, 0.0f, 0.0f);

				if (Math.Abs(rotationY % 360) > epsilon)
					transform = transform.Rotate(rotationY * MathF.PI / 180.0f, 0.0f, 1.0f, 0.0f);

				transform = transform.Rotate(rotation * MathF.PI / 180.0f, 0.0f, 0.0f, 1.0f);

				if (Math.Abs(scaleX - 1) > epsilon || Math.Abs(scaleY - 1) > epsilon)
					transform = transform.Scale(scaleX, scaleY, scale);

				if (Foundation.NSThread.IsMain)
				{
					if (layer != null)
					{
						layer.AnchorPoint = new PointF(anchorX, anchorY);
						layer.Transform = transform;
					}
				}
				else
				{
					CoreFoundation.DispatchQueue.MainQueue.DispatchAsync(() =>
					{
						if (layer != null)
						{
							layer.AnchorPoint = new PointF(anchorX, anchorY);
							layer.Transform = transform;
						}
					});
				}
			}

			// TODO: Use the thread var when porting the Device class.

			Update();
		}

		/// <summary>
		/// Restores all subviews to their natural Z-position hierarchy (Z = 0).
		/// </summary>
		/// <param name="layer">The current layer being processed.</param>
		/// <param name="platformView">The platform view whose container will be restored.</param>
		static void RestoreNaturalZOrder(CALayer layer, UIView platformView)
		{
			var superview = platformView.Superview;
			if (superview?.Subviews is not null)
			{
				var subviews = superview.Subviews;
				var currentIndex = Array.IndexOf(subviews, platformView);

				bool anySiblingRotating = HasAnySibling3DRotation(subviews, platformView);

				if (!anySiblingRotating && currentIndex >= 0)
				{
					// Restore Z-positions for ALL subviews to 0 (natural flat hierarchy)
					for (int i = 0; i < subviews.Length; i++)
					{
						var siblingView = subviews[i];
						if (siblingView?.Layer != null)
						{
							siblingView.Layer.ZPosition = 0;
						}
					}
				}
			}
			else
			{
				layer.ZPosition = 0;
			}
		}

		/// <summary>
		/// Determines whether any subviews have Z-positions modified by the 3D rotation algorithm.
		/// </summary>
		/// <param name="platformView">The platform view to check.</param>
		/// <returns>True if modified Z-positions are detected; otherwise, false.</returns>
		static bool HasModifiedZPositions(UIView platformView)
		{
			var containerView = platformView.Superview;
			if (containerView?.Subviews is null || containerView.Subviews.Length <= 1)
				return false;

			// Iterate through all subviews to detect 3D rotation Z-position patterns
			for (int index = 0; index < containerView.Subviews.Length; index++)
			{
				var subview = containerView.Subviews[index];
				if (subview?.Layer == null)
					continue;

				var currentZPosition = subview.Layer.ZPosition;

				// Calculate expected Z-position for rotating views (BaseOffset pattern)
				var expectedRotatingViewZPosition = index * ZPositionSpacing + ZPositionBaseOffset;

				// Check for 3D rotation Z-position patterns
				bool isRotatingViewPattern = Math.Abs(currentZPosition - expectedRotatingViewZPosition) < 1.0f;
				bool isBehindViewPattern = currentZPosition >= ZPositionSiblingOffset;

				if (isRotatingViewPattern || isBehindViewPattern)
					return true;
			}

			return false;
		}

		static bool HasAnySibling3DRotation(UIView[] subviews, UIView excludeView)
		{
			foreach (var siblingView in subviews)
			{
				if (siblingView != excludeView && siblingView.Layer is not null)
				{
					if (HasLayer3DRotation(siblingView.Layer))
					{
						return true;
					}
				}
			}
			return false;
		}

		static bool HasLayer3DRotation(CALayer layer)
		{
			const double epsilon = 0.001;
			var transform = layer.Transform;
			
			return Math.Abs(transform.M34) > epsilon ||
			       Math.Abs(transform.M13) > epsilon ||
			       Math.Abs(transform.M23) > epsilon ||
			       Math.Abs(transform.M31) > epsilon ||
			       Math.Abs(transform.M32) > epsilon;
		}
	}
}