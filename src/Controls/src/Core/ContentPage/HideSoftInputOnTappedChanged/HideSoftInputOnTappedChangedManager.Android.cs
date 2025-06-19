using System;
using Android.Views;
using Android.Widget;
using Microsoft.Maui.Graphics;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;

namespace Microsoft.Maui.Controls
{
	partial class HideSoftInputOnTappedChangedManager
	{
		internal event EventHandler<MotionEvent?>? DispatchTouchEvent;

		void OnWindowDispatchedTouch(object? sender, MotionEvent? e)
		{
			if (!FeatureEnabled || e is null)
			{
				return;
			}

			foreach (var page in _contentPages)
			{
				if (page.HasNavigatedTo &&
					page.HideSoftInputOnTapped &&
					page.Handler is IPlatformViewHandler pvh &&
					pvh.MauiContext?.Context is not null)
				{
					var location = pvh.PlatformView.GetBoundingBox();
					var androidContext = pvh.MauiContext.Context;

					var point =
						new Point
						(
							androidContext.FromPixels(e.RawX),
							androidContext.FromPixels(e.RawY)
						);

					if (location.Contains(point))
					{
						DispatchTouchEvent?.Invoke(this, e);
						
						if (e.Action == MotionEventActions.Down && pvh.PlatformView is not null)
						{
							HandleGlobalTapForKeyboardDismissal(pvh.PlatformView, e, androidContext);
						}
					}
				}
			}
		}

		void HandleGlobalTapForKeyboardDismissal(AView rootView, MotionEvent e, Android.Content.Context context)
		{
			var focusedView = rootView.FindFocus();
			if (focusedView != null && focusedView.IsSoftInputShowing())
			{
				var focusedLocation = focusedView.GetBoundingBox();
				var tapPoint = new Point(
					context.FromPixels(e.RawX),
					context.FromPixels(e.RawY)
				);

				if (!focusedLocation.Contains(tapPoint))
				{
					rootView.Post(() =>
					{
						if (focusedView.IsSoftInputShowing())
						{
							focusedView.HideSoftInput();
						}
					});
				}
			}
		}

		// This is called from InputViews as they are added to the visual tree
		IDisposable? SetupHideSoftInputOnTapped(AView aView)
		{
			if (aView is AViewGroup vg &&
				vg.GetFirstChildOfType<EditText>() is { } editText)
			{
				aView = editText;
			}

			if (!FeatureEnabled)
				return null;

			var tracker = new TapWindowTracker(aView, this);
			return new ActionDisposable(() =>
			{
				tracker.Disconnect();
				tracker = null;
			});
		}
	}
}
