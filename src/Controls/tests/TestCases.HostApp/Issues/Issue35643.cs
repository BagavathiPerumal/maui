using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Maui.Controls.Sample.Issues
{
	[Issue(IssueTracker.Github, 35643, "CurrentItem is updated incorrectly on Android when the CarouselView is bound to an ObservableCollection with Loop = false", PlatformAffected.Android)]
	public class Issue35643 : ContentPage
	{
		readonly Issue35643ViewModel _viewModel;
		readonly CarouselView _carousel;

		public Issue35643()
		{
			_viewModel = new Issue35643ViewModel
			{
				Items = new ObservableCollection<string> { "0", "1", "2", "3", "4" },
				CurrentItem = "2"
			};

			var currentItemLabel = new Label
			{
				AutomationId = "CurrentItemLabel",
				FontSize = 24,
				HorizontalTextAlignment = TextAlignment.Center
			};
			currentItemLabel.SetBinding(Label.TextProperty, new Binding(nameof(Issue35643ViewModel.CurrentItem)));

			var positionLabel = new Label
			{
				AutomationId = "PositionLabel",
				FontSize = 18,
				HorizontalTextAlignment = TextAlignment.Center
			};

			_carousel = new CarouselView
			{
				Loop = false,
				AutomationId = "CarouselView",
				ItemTemplate = new DataTemplate(() =>
				{
					var label = new Label
					{
						HorizontalTextAlignment = TextAlignment.Center,
						VerticalTextAlignment = TextAlignment.Center,
						FontSize = 20
					};
					label.SetBinding(Label.TextProperty, ".");
					return new Frame { Content = label, BackgroundColor = Colors.LightBlue };
				})
			};
			_carousel.SetBinding(CarouselView.ItemsSourceProperty, new Binding(nameof(Issue35643ViewModel.Items)));
			_carousel.SetBinding(CarouselView.CurrentItemProperty, new Binding(nameof(Issue35643ViewModel.CurrentItem)));

			// Use a direct binding so PositionLabel reflects the current Position at all times,
			// including at startup (event-only handlers miss the initial value).
			positionLabel.SetBinding(Label.TextProperty, new Binding("Position", source: _carousel));

			// Replace current item (non-last): items[2] = "2b", current stays at index 2
			var updateButton = new Button
			{
				Text = "Replace current item (non-last)",
				AutomationId = "UpdateButton"
			};
			updateButton.Clicked += (s, e) =>
			{
				_viewModel.Items[2] = "2b";
				_viewModel.CurrentItem = "2b";
			};

			// Replace a non-current iteZm (first): items[0] = "0b", position must not reset
			var replaceNonCurrentButton = new Button
			{
				Text = "Replace non-current item (first)",
				AutomationId = "ReplaceNonCurrentButton"
			};
			replaceNonCurrentButton.Clicked += (s, e) =>
			{
				_viewModel.Items[0] = "0b";
			};

			// Reset to initial state so tests can be run independently
			var resetButton = new Button
			{
				Text = "Reset",
				AutomationId = "ResetButton"
			};
			resetButton.Clicked += (s, e) =>
			{
				_viewModel.Items = new ObservableCollection<string> { "0", "1", "2", "3", "4" };
				_viewModel.CurrentItem = "2";
			};

			BindingContext = _viewModel;

			var layout = new Grid
			{
				Padding = new Thickness(10),
				RowSpacing = 10,
				RowDefinitions =
				{
					new RowDefinition { Height = GridLength.Auto },
					new RowDefinition { Height = GridLength.Auto },
					new RowDefinition { Height = GridLength.Auto },
					new RowDefinition { Height = 200 },
					new RowDefinition { Height = GridLength.Auto },
					new RowDefinition { Height = GridLength.Auto },
					new RowDefinition { Height = GridLength.Auto },
				}
			};

			var currentItemHeader = new Label { Text = "Current Item:" };
			Grid.SetRow(currentItemHeader, 0);
			Grid.SetRow(currentItemLabel, 1);
			Grid.SetRow(_carousel, 3);
			Grid.SetRow(updateButton, 4);
			Grid.SetRow(replaceNonCurrentButton, 5);
			Grid.SetRow(resetButton, 6);

			layout.Children.Add(currentItemHeader);
			layout.Children.Add(currentItemLabel);
			layout.Children.Add(_carousel);
			layout.Children.Add(updateButton);
			layout.Children.Add(replaceNonCurrentButton);
			layout.Children.Add(resetButton);

			// Position row: "Position: <value>" in row 2
			var positionRow = new HorizontalStackLayout { Spacing = 8 };
			positionRow.Children.Add(new Label { Text = "Position:" });
			positionRow.Children.Add(positionLabel);
			Grid.SetRow(positionRow, 2);
			layout.Children.Add(positionRow);

			Content = layout;
		}
	}

	public class Issue35643ViewModel : INotifyPropertyChanged
	{
		ObservableCollection<string> _items;
		string _currentItem;

		public ObservableCollection<string> Items
		{
			get => _items;
			set => SetProperty(ref _items, value);
		}

		public string CurrentItem
		{
			get => _currentItem;
			set => SetProperty(ref _currentItem, value);
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
		{
			if (EqualityComparer<T>.Default.Equals(backingStore, value))
				return false;
			backingStore = value;
			OnPropertyChanged(propertyName);
			return true;
		}
	}
}
