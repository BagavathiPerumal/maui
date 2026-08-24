using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;

namespace Maui.Controls.Sample.Issues;

[Issue(IssueTracker.Github, 34020, "ScrollView doesn't work with multiple Editor controls on a Page", PlatformAffected.Android)]
public class Issue34020 : NavigationPage
{
	public Issue34020() : base(new Issue34020Content())
	{
	}
}

public class Issue34020Content : ContentPage
{
	public Issue34020Content()
	{
		Title = "Issue 34020";

#if ANDROID
		// Ensures the toolbar remains visible and reachable when the on-screen keyboard
		// appears, by resizing the window instead of panning it.
		Microsoft.Maui.Controls.Application.Current?
			.On<Microsoft.Maui.Controls.PlatformConfiguration.Android>()
			.UseWindowSoftInputModeAdjust(WindowSoftInputModeAdjust.Resize);
#endif

		ToolbarItems.Add(new ToolbarItem
		{
			Text = "Hide keyboard",
			AutomationId = "ToolbarHideKeyboard",
			Order = ToolbarItemOrder.Primary,
			Command = new Command(() => Content.Unfocus())
		});

		var topMarker = new Label
		{
			Text = "TOP MARKER",
			AutomationId = "TopMarker",
			FontSize = 20,
			BackgroundColor = Colors.LightGreen
		};

		var editor1 = new Editor
		{
			HeightRequest = 180,
			Placeholder = "Top Editor",
			AutomationId = "TopEditor"
		};

		var editor2 = new Editor
		{
			HeightRequest = 180,
			Placeholder = "Middle Editor",
			AutomationId = "MiddleEditor"
		};

		var editor3 = new Editor
		{
			HeightRequest = 180,
			Placeholder = "Bottom Editor",
			AutomationId = "BottomEditor"
		};

		var editor4 = new Editor
		{
			HeightRequest = 180,
			Placeholder = "Editor 4",
			AutomationId = "Editor4"
		};

		var editor5 = new Editor
		{
			HeightRequest = 180,
			Placeholder = "Editor 5 (last)",
			AutomationId = "Editor5"
		};

		var bottomMarker = new Label
		{
			Text = "BOTTOM MARKER",
			AutomationId = "BottomMarker",
			FontSize = 20,
			BackgroundColor = Colors.LightCoral
		};

		var stack = new VerticalStackLayout
		{
			Padding = 20,
			Spacing = 20,
			Children =
			{
				topMarker,
				editor1,
				editor2,
				editor3,
				editor4,
				editor5,
				bottomMarker
			}
		};

		Content = new ScrollView
		{
			AutomationId = "PageScrollView",
			Content = stack
		};
	}
}
