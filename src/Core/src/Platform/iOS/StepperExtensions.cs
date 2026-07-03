using System;
using ObjCRuntime;
using UIKit;
using CoreGraphics;

namespace Microsoft.Maui.Platform
{
	public static class StepperExtensions
	{
		public static void UpdateMinimum(this UIStepper platformStepper, IStepper stepper)
		{
			platformStepper.MinimumValue = stepper.Minimum;
		}

		public static void UpdateMaximum(this UIStepper platformStepper, IStepper stepper)
		{
			platformStepper.MaximumValue = stepper.Maximum;
		}

		public static void UpdateIncrement(this UIStepper platformStepper, IStepper stepper)
		{
			var increment = stepper.Interval;

			if (increment > 0)
				platformStepper.StepValue = stepper.Interval;
		}

		public static void UpdateValue(this UIStepper platformStepper, IStepper stepper)
		{
			// Update MinimumValue first to prevent UIStepper from incorrectly clamping the Value.
			// If MAUI updates Value before Minimum, a stale higher MinimumValue would cause iOS to clamp Value incorrectly.
			if (platformStepper.MinimumValue != stepper.Minimum)
			{
				platformStepper.MinimumValue = stepper.Minimum;
			}

			if (platformStepper.Value != stepper.Value)
			{
				platformStepper.Value = stepper.Value;
			}

		}

		// Applies the semantic content attribute and visual transform
		// to the UIStepper and its subviews based on the Stepper's FlowDirection
		// and its parent's layout direction.
		internal static void UpdateFlowDirection(this UIStepper platformStepper, IStepper stepper)
		{
			UISemanticContentAttribute contentAttribute = GetSemanticContentAttribute(stepper);
			// iOS 26 changed UIStepper internal rendering so that SemanticContentAttribute alone
			// is no longer sufficient to mirror the control for RTL. A horizontal transform is required.
			bool isIOS26 = OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26);
			platformStepper.SemanticContentAttribute = contentAttribute;

			// Apply transform to stepper subviews on iOS 26+, but only when mirroring is
			// actually required (RTL). Touching UIStepper.Subviews forces UIKit to lazily
			// realize the internal Liquid Glass button views (a native, render-server backed
			// hierarchy). For the common LTR/identity-transform case this realization has no
			// visual effect but leaves the UIStepper with a persistent native retain that
			// outlives the .NET GC cycles used by the leak tests (see
			// https://github.com/dotnet/maui/issues/35985). Skipping the subview walk entirely
			// unless the control needs to be mirrored avoids triggering that retain in the
			// overwhelmingly common non-RTL case.
			if (isIOS26)
			{
				CGAffineTransform transform = GetCGAffineTransform(stepper);
				bool needsMirroring = !transform.IsIdentity;

				if (needsMirroring)
				{
					// Setting Transform on UIStepper (and its internal Liquid Glass button subviews)
					// implicitly creates a CoreAnimation animation on iOS 26+. That implicit
					// animation retains the view via the render server for the animation's duration,
					// which can outlast a GC cycle and delay/prevent collection. Wrapping the
					// assignment in UIView.PerformWithoutAnimation disables the implicit animation
					// so no extra native retain is taken.
					UIView.PerformWithoutAnimation(() =>
					{
						platformStepper.Transform = transform;

						foreach (var subview in platformStepper.Subviews)
						{
							subview.SemanticContentAttribute = contentAttribute;
							subview.Transform = transform;
						}
					});
				}
				else if (!platformStepper.Transform.IsIdentity)
				{
					// The stepper previously had a mirrored transform applied but no longer
					// needs it (e.g. FlowDirection changed back to LTR) - restore identity
					// without forcing a fresh subview realization pass beyond what UIKit
					// already has in place.
					UIView.PerformWithoutAnimation(() =>
					{
						platformStepper.Transform = transform;
					});
				}
			}
			else
			{
				foreach (var subview in platformStepper.Subviews)
				{
					subview.SemanticContentAttribute = contentAttribute;
				}
			}
		}

		static UISemanticContentAttribute GetSemanticContentAttribute(IStepper stepper)
		{
			return stepper.FlowDirection switch
			{
				FlowDirection.LeftToRight => UISemanticContentAttribute.ForceLeftToRight,
				FlowDirection.RightToLeft => UISemanticContentAttribute.ForceRightToLeft,
				_ => GetParentSemanticContentAttribute(stepper),
			};
		}

		static UISemanticContentAttribute GetParentSemanticContentAttribute(IStepper stepper)
		{
			var parentView = (stepper as IView)?.Parent as IView;
			if (parentView is null)
			{
				return UISemanticContentAttribute.Unspecified;
			}

			return parentView.FlowDirection switch
			{
				FlowDirection.LeftToRight => UISemanticContentAttribute.ForceLeftToRight,
				FlowDirection.RightToLeft => UISemanticContentAttribute.ForceRightToLeft,
				_ => UISemanticContentAttribute.Unspecified,
			};
		}

		static CGAffineTransform GetCGAffineTransform(IStepper stepper)
		{
			return stepper.FlowDirection switch
			{
				FlowDirection.LeftToRight => CGAffineTransform.MakeIdentity(),
				FlowDirection.RightToLeft => CGAffineTransform.MakeScale(-1, 1),
				_ => GetParentTransform(stepper), // Default to parent's direction if MatchParent
			};
		}

		static CGAffineTransform GetParentTransform(IStepper stepper)
		{
			var parentSemanticAttribute = GetParentSemanticContentAttribute(stepper);
			if (parentSemanticAttribute == UISemanticContentAttribute.ForceRightToLeft)
			{
				// Flip horizontally for RTL
				return CGAffineTransform.MakeScale(-1, 1);
			}
			// Identity transform for LTR
			return CGAffineTransform.MakeIdentity();
		}
	}
}
