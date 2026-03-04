#if IOS || MACCATALYST
using Microsoft.Maui.Platform;
using UIKit;
#endif

namespace Maui.Controls.Sample.Issues;

[Issue(IssueTracker.Github, 33689, "MacCatalyst UISheetPresentationController ignores custom detent — presents as full modal", PlatformAffected.macOS)]
public class Issue33689 : ContentPage
{
	readonly Label _measuredHeightLabel;

	public Issue33689()
	{
		_measuredHeightLabel = new Label
		{
			AutomationId = "MeasuredHeight",
			Text = "Pending"
		};

		var showButton = new Button
		{
			Text = "ShowSheet",
			AutomationId = "ShowSheetButton",
			HorizontalOptions = LayoutOptions.Fill
		};
		showButton.Clicked += OnShowSheetClicked;

		Content = new VerticalStackLayout
		{
			Padding = 10,
			Spacing = 8,
			Children =
			{
				showButton,
				new Label { Text = "Measured height:" },
				_measuredHeightLabel
			}
		};
	}

	void OnShowSheetClicked(object sender, EventArgs e)
	{
#if IOS || MACCATALYST
		// Content: plain VerticalStackLayout with Labels — no CollectionView.
		var content = new Issue33689SheetContent();

		var mauiContext = this.Handler?.MauiContext ?? throw new Exception("MauiContext is null");
		var parent = this.ToUIViewController(mauiContext);
		var vcToPresent = content.ToUIViewController(mauiContext);

		var display = DeviceDisplay.MainDisplayInfo;
		double widthDip = display.Width / display.Density;
		double heightDip = display.Height / display.Density;

		// Measure() BEFORE mounting — VerticalStackLayout measures correctly (unlike CollectionView2 bug).
		var measuredSize = content.Measure(widthDip, heightDip);
		_measuredHeightLabel.Text = measuredSize.Height.ToString("F0");

		// Explicitly set a custom detent to the measured content height.
		// On iOS: the sheet resizes to this height (partial sheet) — custom detent works.
		// On MacCatalyst: the sheet still presents full-height — custom detent is ignored by the platform.
		if (OperatingSystem.IsIOSVersionAtLeast(16) || OperatingSystem.IsMacCatalystVersionAtLeast(16))
		{
			var detent = UISheetPresentationControllerDetent.Create(
				"ContentHeight",
				(ctx) => (nfloat)measuredSize.Height);

			var sheet = vcToPresent.SheetPresentationController;
			if (sheet is not null)
			{
				sheet.Detents = new[] { detent };
				sheet.LargestUndimmedDetentIdentifier = UISheetPresentationControllerDetentIdentifier.Unknown;
				sheet.PrefersScrollingExpandsWhenScrolledToEdge = false;
				sheet.PrefersEdgeAttachedInCompactHeight = true;
				sheet.WidthFollowsPreferredContentSizeWhenEdgeAttached = true;
			}
		}

		parent.PresentViewController(vcToPresent, animated: true, completionHandler: null);
#endif
	}
}

// Simple content view with only Labels — no CollectionView.
public class Issue33689SheetContent : Microsoft.Maui.Controls.ContentView
{
	public Issue33689SheetContent()
	{
		BackgroundColor = Colors.LightBlue;

		var layout = new VerticalStackLayout
		{
			Padding = new Thickness(16),
			Spacing = 12
		};

		for (int i = 1; i <= 5; i++)
		{
			layout.Children.Add(new Label
			{
				Text = "Sheet Item #" + i,
				AutomationId = "Sheet Item #" + i,
				FontSize = 18
			});
		}

		Content = layout;
	}
}
