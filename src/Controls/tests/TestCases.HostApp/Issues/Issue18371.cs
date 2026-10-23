namespace Maui.Controls.Sample.Issues;

[Issue(IssueTracker.Github, 18371, "Shell navigation bar color should be ready during modal dismissal", PlatformAffected.All)]
public class Issue18371 : TestShell
{
	protected override void Init()
	{
		FlyoutBehavior = FlyoutBehavior.Disabled;
		Shell.SetBackgroundColor(this, Color.FromArgb("#512BD4"));
		Shell.SetTitleColor(this, Colors.White);

		var openModal = new Button { AutomationId = "OpenWelcome", Text = "Press me!", Margin = 32 };
		var mainPage = new ContentPage
		{
			Title = "Home",
			Content = new VerticalStackLayout
			{
				VerticalOptions = LayoutOptions.Center,
				Children = { openModal }
			}
		};
		AddFlyoutItem(mainPage, "Issue18371Main");
		var startupPage = new ContentPage { Title = "Startup" };
		Shell.SetNavBarIsVisible(startupPage, false);
		AddFlyoutItem(startupPage, "Issue18371Startup");
		Routing.RegisterRoute("Issue18371Welcome", typeof(WelcomePage));

		openModal.Clicked += async (_, _) =>
		{
			await GoToAsync("//Issue18371Startup/Issue18371Welcome");
			var modal = (WelcomePage)CurrentPage;
			modal.ReturnButton.Clicked += async (_, _) => await GoToAsync("//Issue18371Main");
		};
	}

	public class WelcomePage : ContentPage
	{
		public Button ReturnButton { get; } = new Button
		{
			AutomationId = "ReturnHome",
			Text = "Now press me!",
			Margin = 32
		};

		public WelcomePage()
		{
			Shell.SetPresentationMode(this, PresentationMode.Modal);
			Content = new VerticalStackLayout
			{
				VerticalOptions = LayoutOptions.End,
				Children = { ReturnButton }
			};
		}
	}
}
