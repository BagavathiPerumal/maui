#if ANDROID // This regression is specific to Android's Loaded event and native drawing lifecycle.
using Android.Runtime;
using Android.Views;
using AView = Android.Views.View;

namespace Maui.Controls.Sample.Issues;

[Issue(IssueTracker.Github, 38360, "Android: Behavior Change in the Loaded Event Observed in Version 10.0.60+", PlatformAffected.Android)]
public class Issue38360 : ContentPage
{
	public Issue38360()
	{
		var resultLabel = new Label
		{
			Text = "Waiting...",
			AutomationId = "ResultLabel",
			FontSize = 18
		};

		var testView = new BoxView
		{
			AutomationId = "TestView",
			WidthRequest = 100,
			HeightRequest = 100,
			Color = Colors.Red
		};

		var hasDrawn = false;
		ViewTreeObserver observer = null;
		OnDrawListener drawListener = null;

		testView.HandlerChanged += (_, _) =>
		{
			if (testView.Handler?.PlatformView is not AView nativeView)
				return;

			observer = nativeView.ViewTreeObserver;
			drawListener = new OnDrawListener(() => hasDrawn = true);
			observer.AddOnDrawListener(drawListener);
		};

		testView.Loaded += (_, _) =>
			resultLabel.Text = hasDrawn ? "DrawnBeforeLoaded" : "LoadedBeforeDraw";

		Disappearing += (_, _) =>
		{
			if (observer?.IsAlive == true && drawListener is not null)
				observer.RemoveOnDrawListener(drawListener);

			drawListener?.Dispose();
		};

		Content = new VerticalStackLayout
		{
			Padding = 20,
			Spacing = 10,
			Children = { resultLabel, testView }
		};
	}

	sealed class OnDrawListener : Java.Lang.Object, ViewTreeObserver.IOnDrawListener
	{
		Action _onDraw;

		public OnDrawListener(Action onDraw)
		{
			_onDraw = onDraw;
		}

		OnDrawListener(IntPtr handle, JniHandleOwnership transfer)
			: base(handle, transfer)
		{
		}

		public void OnDraw() => _onDraw?.Invoke();

		protected override void Dispose(bool disposing)
		{
			_onDraw = null;
			base.Dispose(disposing);
		}
	}
}
#endif
