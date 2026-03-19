namespace Maui.Controls.Sample.Issues;

[Issue(IssueTracker.Github, 33508, "Android: BackButton on Android 16 not working after command from FlyOutPage", PlatformAffected.Android)]
public class Issue33508 : ContentPage
{
	readonly Issue33508NavigationService _navigationService;
	readonly Issue33508State _state;

	public Issue33508()
	{
		_state = new Issue33508State();
		_navigationService = new Issue33508NavigationService(_state);
		Content = CreateStartPageContent();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_navigationService.NavigateToStartPage();
	}

	VerticalStackLayout CreateStartPageContent()
	{
		var lastNavigationSourceLabel = new Label
		{
			AutomationId = "Issue33508LastNavigationSourceLabel"
		};

		_state.Changed += (_, _) =>
			lastNavigationSourceLabel.Text = $"Last navigation source: {_state.LastNavigationSource}";

		lastNavigationSourceLabel.Text = $"Last navigation source: {_state.LastNavigationSource}";

		return new VerticalStackLayout
		{
			Padding = 24,
			Spacing = 16,
			Children =
			{
				new Label
				{
					Text = "StartPage",
					AutomationId = "Issue33508StartPageLabel"
				},
				new Label
				{
					Text = "Navigate to DetailPage1, then use Android back to trigger FlyoutPage.OnBackButtonPressed.",
					AutomationId = "Issue33508StartPageInstructionLabel"
				},
				lastNavigationSourceLabel,
				new Button
				{
					Text = "Go to DetailPage1",
					AutomationId = "Issue33508NavigateToDetailButton",
					Command = new Command(() => _navigationService.NavigateToDetailPage())
				}
			}
		};
	}
}

sealed class Issue33508NavigationService
{
	readonly Issue33508State _state;

	public Issue33508NavigationService(Issue33508State state)
	{
		_state = state;
	}

	public void NavigateToStartPage(string source = "Launch")
	{
		_state.LastNavigationSource = source;
		if (Application.Current?.Windows.FirstOrDefault() is Window window)
			window.Page = CreateStartPage();
	}

	public void NavigateToDetailPage()
	{
		if (Application.Current?.Windows.FirstOrDefault() is not Window window)
		{
			return;
		}

		if (window.Page is not Issue33508MasterPage masterPage)
		{
			masterPage = new Issue33508MasterPage(this, _state);
			window.Page = masterPage;
		}

		masterPage.Detail = new NavigationPage(CreateDetailPage());
	}

	ContentPage CreateStartPage()
	{
		var lastNavigationSourceLabel = new Label
		{
			AutomationId = "Issue33508LastNavigationSourceLabel"
		};

		void RefreshStartPage(object sender, EventArgs e) =>
			lastNavigationSourceLabel.Text = $"Last navigation source: {_state.LastNavigationSource}";

		_state.Changed += RefreshStartPage;
		lastNavigationSourceLabel.Text = $"Last navigation source: {_state.LastNavigationSource}";

		var page = new ContentPage
		{
			Title = "StartPage",
			Content = new VerticalStackLayout
			{
				Padding = 24,
				Spacing = 16,
				Children =
				{
					new Label
					{
						Text = "StartPage",
						AutomationId = "Issue33508StartPageLabel"
					},
					new Label
					{
						Text = "Navigate to DetailPage1, then use Android back to trigger FlyoutPage.OnBackButtonPressed.",
						AutomationId = "Issue33508StartPageInstructionLabel"
					},
					lastNavigationSourceLabel,
					new Button
					{
						Text = "Go to DetailPage1",
						AutomationId = "Issue33508NavigateToDetailButton",
						Command = new Command(NavigateToDetailPage)
					}
				}
			}
		};

		page.Disappearing += (_, _) => _state.Changed -= RefreshStartPage;
		return page;
	}

	ContentPage CreateDetailPage()
	{
		return new Issue33508DetailPage(this, _state);
	}
}

sealed class Issue33508MasterPage : FlyoutPage
{
	readonly Issue33508NavigationService _navigationService;
	readonly Issue33508State _state;

	public Issue33508MasterPage(Issue33508NavigationService navigationService, Issue33508State state)
	{
		_navigationService = navigationService;
		_state = state;
		Title = "MasterPage";
		FlyoutLayoutBehavior = FlyoutLayoutBehavior.Popover;

		Flyout = new ContentPage
		{
			Title = "Flyout Menu",
			Content = new VerticalStackLayout
			{
				Padding = 24,
				Spacing = 16,
				Children =
				{
					new Label
					{
						Text = "This is the MasterPage",
						AutomationId = "Issue33508FlyoutInstructionLabel"
					},
					new Button
					{
						Text = "Click To Navigate To StartPage",
						AutomationId = "Issue33508FlyoutNavigateToStartButton",
						Command = new Command(() => _navigationService.NavigateToStartPage("Flyout"))
					}
				}
			}
		};

		Detail = new NavigationPage(new ContentPage());
	}

	protected override bool OnBackButtonPressed()
	{
		_state.BackHandledCount++;
		_state.LastNavigationSource = "SystemBack";
		_state.NotifyChanged();
		return true;
	}
}

sealed class Issue33508DetailPage : ContentPage
{
	readonly Issue33508NavigationService _navigationService;
	readonly Issue33508State _state;
	readonly Label _backHandledCountLabel;

	public Issue33508DetailPage(Issue33508NavigationService navigationService, Issue33508State state)
	{
		_navigationService = navigationService;
		_state = state;
		Title = "DetailPage1";

		_backHandledCountLabel = new Label
		{
			AutomationId = "Issue33508BackHandledCountLabel"
		};

		_state.Changed += (_, _) => RefreshBackHandledCount();

		Content = new VerticalStackLayout
		{
			Padding = 24,
			Spacing = 16,
			Children =
			{
				new Label
				{
					Text = "DetailPage1",
					AutomationId = "Issue33508DetailPageLabel"
				},
				new Label
				{
					Text = "Press Android back. The page should stay visible and increment the FlyoutPage counter, even after StartPage is reopened directly from the flyout.",
					AutomationId = "Issue33508DetailPageInstructionLabel"
				},
				_backHandledCountLabel,
				new Button
				{
					Text = "Click To Navigate To StartPage",
					AutomationId = "Issue33508DetailNavigateToStartButton",
					Command = new Command(() => _navigationService.NavigateToStartPage("DetailPage"))
				},
				new Button
				{
					Text = "Open Flyout Menu",
					AutomationId = "Issue33508OpenFlyoutButton",
					Command = new Command(() =>
					{
						if (Application.Current?.Windows.FirstOrDefault()?.Page is Issue33508MasterPage masterPage)
						{
							masterPage.IsPresented = true;
						}
					})
				}
			}
		};

		RefreshBackHandledCount();
	}

	void RefreshBackHandledCount()
	{
		_backHandledCountLabel.Text = $"Back handled count: {_state.BackHandledCount}";
	}
}

sealed class Issue33508State
{
	string _lastNavigationSource = "Launch";

	public event EventHandler Changed;

	public int BackHandledCount { get; set; }

	public string LastNavigationSource
	{
		get => _lastNavigationSource;
		set
		{
			_lastNavigationSource = value;
			NotifyChanged();
		}
	}

	public void NotifyChanged()
	{
		Changed?.Invoke(this, EventArgs.Empty);
	}
}
