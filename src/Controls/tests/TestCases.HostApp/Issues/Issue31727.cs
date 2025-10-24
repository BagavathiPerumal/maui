namespace Maui.Controls.Sample.Issues;

[Issue(IssueTracker.Github, 31727, "Toolbaritem keeps the icon of the previous page on Android, using NavigationPage (not shell)", PlatformAffected.Android)]
public class Issue31727 : NavigationPage
{
	public Issue31727() : base(new MainTestPage())
	{
	}

	public class MainTestPage : ContentPage
    {
        int _navigationCount = 0;

        Label _statusLabel;
        Label _instructionLabel;
        Button _rapidNavigationButton;
        Button _resetButton;

        public MainTestPage()
        {
            Title = "Toolbar Icon Race Test";

            ToolbarItems.Add(new ToolbarItem
            {
                Text = "Edit",
                IconImageSource = new FontImageSource
                {
                    FontFamily = MaterialIcons.FontName, 
                    Glyph = MaterialIcons.Edit,          
                    Size = 40,
                    Color = Colors.Black
                },
                Priority = 0
            });

            _statusLabel = new Label
            {
                AutomationId = "StatusLabel",
                Text = "Ready to test",
                FontSize = 16,
                HorizontalOptions = LayoutOptions.Center
            };

            _instructionLabel = new Label
            {
                AutomationId = "InstructionLabel",
                Text = "Tap 'Rapid Navigation' multiple times quickly to trigger race condition.\n" +
                       "Watch toolbar icon color - it should stay correct.",
                FontSize = 14,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center
            };

            _rapidNavigationButton = new Button
            {
                AutomationId = "RapidNavigationButton",
                Text = "Rapid Navigation Test",
                BackgroundColor = Colors.LightBlue,
                TextColor = Colors.Black
            };
            _rapidNavigationButton.Clicked += OnRapidNavigationClicked;

            _resetButton = new Button
            {
                AutomationId = "ResetButton",
                Text = "Reset Test",
                BackgroundColor = Colors.LightGray,
                TextColor = Colors.Black
            };
            _resetButton.Clicked += OnResetClicked;

            var debugLabel = new Label
            {
                AutomationId = "DebugLabel",
                Text = "Debug: Check console logs for [ToolbarExtensions] messages during navigation",
                FontSize = 12,
                TextColor = Colors.Gray,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center
            };

            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Margin = 20,
                    Spacing = 20,
                    Children =
                    {
                        _statusLabel,
                        _instructionLabel,
                        _rapidNavigationButton,
                        _resetButton,
                        debugLabel
                    }
                }
            };
        }

        async void OnRapidNavigationClicked(object sender, EventArgs e)
        {
            _navigationCount++;
            _statusLabel.Text = $"Navigation #{_navigationCount} - Checking for race conditions...";

            try
            {
                await Navigation.PushAsync(new Issue31727EditPage(_navigationCount));
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Navigation failed: {ex.Message}";
            }
        }

        void OnResetClicked(object sender, EventArgs e)
        {
            _navigationCount = 0;
            _statusLabel.Text = "Test reset - Ready to test";
        }
    }

	public class Issue31727EditPage : ContentPage
    {
        readonly int _pageNumber;
        Label _pageLabel;
        Button _goToValidateButton;
        Button _backButton;

        public Issue31727EditPage(int pageNumber)
        {
            _pageNumber = pageNumber;
            Title = $"Edit Page #{pageNumber}";

            AddToolbarItems();
            BuildLayout();
        }

        void AddToolbarItems()
        {
            ToolbarItems.Add(new ToolbarItem
            {
                IsEnabled = false,
                Text = "Delete",
                IconImageSource = new FontImageSource
                {
                    FontFamily = MaterialIcons.FontName,
                    Glyph = MaterialIcons.Delete,
                    Size = 40,
                    Color = Colors.Black
                },
                Priority = 1
            });

            ToolbarItems.Add(new ToolbarItem
            {
                Text = "Edit",
                IconImageSource = new FontImageSource
                {
                    FontFamily = MaterialIcons.FontName,
                    Glyph = MaterialIcons.Edit,
                    Size = 40,
                    Color = Colors.Black
                },
                Priority = 2
            });

            ToolbarItems.Add(new ToolbarItem
            {
                IsEnabled = false,
                Text = "Validate",
                IconImageSource = new FontImageSource
                {
                    FontFamily = MaterialIcons.FontName,
                    Glyph = MaterialIcons.Check,
                    Size = 40,
                    Color = Colors.Black
                },
                Priority = 3
            });
        }

        void BuildLayout()
        {
            _pageLabel = new Label
            {
                AutomationId = "EditPageLabel",
                Text = $"Edit Page #{_pageNumber}\nWatch toolbar icons during navigation",
                FontSize = 16,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(10, 20)
            };

            _goToValidateButton = new Button
            {
                AutomationId = "GoToValidateButton",
                Text = "Go to Validate (Quick!)",
                BackgroundColor = Colors.Orange,
                TextColor = Colors.Black,
                Margin = new Thickness(20, 10)
            };
            _goToValidateButton.Clicked += OnGoToValidateClicked;

            _backButton = new Button
            {
                AutomationId = "BackToMainButton",
                Text = "Back to Main",
                BackgroundColor = Colors.LightGray,
                TextColor = Colors.Black,
                Margin = new Thickness(20, 10)
            };
            _backButton.Clicked += OnBackClicked;

            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Margin = 25,
                    Spacing = 25,
                    Children =
                    {
                        _pageLabel,
                        _goToValidateButton,
                        _backButton
                    }
                }
            };
        }

        async void OnGoToValidateClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new Issue31727ValidatePage(_pageNumber));
        }

        async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }

	public class Issue31727ValidatePage : ContentPage
    {
        readonly int _pageNumber;

        public Issue31727ValidatePage(int pageNumber)
        {
            _pageNumber = pageNumber;
            Title = $"Validate Page #{pageNumber}";

            ToolbarItems.Add(new ToolbarItem
            {
                Priority = 1,
                Text = "Cancel",
                IconImageSource = new FontImageSource
                {
                    FontFamily = MaterialIcons.FontName,
                    Glyph = MaterialIcons.Close,
                    Size = 40,
                    Color = Colors.Black
                }
            });

            ToolbarItems.Add(new ToolbarItem
            {
                Priority = 2,
                Text = "Validate",
                IconImageSource = new FontImageSource
                {
                    FontFamily = MaterialIcons.FontName,
                    Glyph = MaterialIcons.Check,
                    Size = 40,
                    Color = Colors.Black
                }
            });

            var label = new Label
            {
                AutomationId = "ValidatePageLabel",
                Text = $"Validate Page #{_pageNumber}\n\nIcons should appear correct.",
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                FontSize = 16,
                Margin = new Thickness(10, 20)
            };

            var rapidBackButton = new Button
            {
                AutomationId = "RapidBackButton",
                Text = "Rapid Back Navigation",
                BackgroundColor = Colors.Pink,
                TextColor = Colors.Black,
                Margin = new Thickness(20, 10)
            };
            rapidBackButton.Clicked += async (s, e) => await Navigation.PopAsync();

            var backToMainButton = new Button
            {
                AutomationId = "BackToMainFromValidateButton",
                Text = "Back to Main",
                BackgroundColor = Colors.LightGray,
                TextColor = Colors.Black,
                Margin = new Thickness(20, 10)
            };
            backToMainButton.Clicked += async (s, e) => await Navigation.PopToRootAsync();

            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Margin = new Thickness(25),
                    Spacing = 25,
                    Children =
                    {
                        label,
                        rapidBackButton,
                        backToMainButton,

                        new Label
                        {
                            Text = "Fake Form:",
                            HorizontalTextAlignment = TextAlignment.Center
                        },

                        CreateListOfEntries()
                    }
                }
            };
        }

        View CreateListOfEntries()
        {
            var layout = new VerticalStackLayout { Spacing = 15 };

            for (int i = 0; i < 15; i++)
            {
                layout.Children.Add(new Label { Text = $"Entry {i + 1}" });
                layout.Children.Add(new Entry());
            }

            return layout;
        }
    }
}