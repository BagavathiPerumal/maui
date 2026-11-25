namespace Maui.Controls.Sample.Issues;

[Issue(IssueTracker.Github, 37333, "OnSizeAllocated is called twice when navigating to a Shell page", PlatformAffected.Android)]
public class Issue37333 : Shell
{
	public Issue37333()
	{
		Routing.RegisterRoute(nameof(Issue37333TextPage), typeof(Issue37333TextPage));

		Items.Add(new ShellContent
		{
			Title = "Home",
			Route = "MainPage",
			Content = new Issue37333MainPage()
		});
	}
}

public class Issue37333MainPage : Issue37333AllocationPage
{
	public Issue37333MainPage() : base("MainPageAllocationCount")
	{
		var showAllButton = new Button
		{
			AutomationId = "ShowAllButton",
			Text = "Show all"
		};
		showAllButton.Clicked += async (_, _) => await Shell.Current.GoToAsync(nameof(Issue37333TextPage));

		Content = new ScrollView
		{
			Content = new VerticalStackLayout
			{
				Padding = new Thickness(30, 0),
				Spacing = 25,
				Children =
				{
					AllocationCountLabel,
					new Label { HeightRequest = 185 },
					new Label { Text = "Hello, World!" },
					showAllButton
				}
			}
		};
	}

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		UpdateAllocationCount();
	}
}

public class Issue37333TextPage : Issue37333AllocationPage
{
	public Issue37333TextPage() : base("TextPageAllocationCount")
	{
		Content = new VerticalStackLayout
		{
			Padding = 24,
			Spacing = 16,
			Children =
			{
				AllocationCountLabel,
				new Label { Text = "Text page" },
				new Editor
				{
					Text = "This page reproduces the Shell navigation scenario.",
					AutoSize = EditorAutoSizeOption.TextChanges
				}
			}
		};
	}

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		UpdateAllocationCount();
	}
}

public abstract class Issue37333AllocationPage : ContentPage
{
	int _sizeAllocatedCount;

	protected Label AllocationCountLabel { get; }

	protected Issue37333AllocationPage(string automationId)
	{
		SafeAreaEdges = SafeAreaEdges.Container;
		AllocationCountLabel = new Label
		{
			AutomationId = automationId,
			Text = "0"
		};
	}

	protected void UpdateAllocationCount() =>
		AllocationCountLabel.Text = (++_sizeAllocatedCount).ToString();
}
