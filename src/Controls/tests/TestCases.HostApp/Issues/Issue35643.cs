using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Maui.Controls.Sample.Issues
{
	[Issue(IssueTracker.Github, 35643, "CurrentItem is updated incorrectly on Android when the CarouselView is bound to an ObservableCollection with Loop = false", PlatformAffected.Android)]
	public class Issue35643 : ContentPage
	{
		readonly Issue35643ViewModel _viewModel;

		public Issue35643()
		{
			_viewModel = new Issue35643ViewModel
			{
				Items = new ObservableCollection<string> { "0", "1", "2" },
				CurrentItem = "2"
			};

			var currentItemLabel = new Label
			{
				AutomationId = "CurrentItemLabel",
				FontSize = 24,
				HorizontalTextAlignment = TextAlignment.Center
			};
			currentItemLabel.SetBinding(Label.TextProperty, new Binding(nameof(Issue35643ViewModel.CurrentItem)));

			var carousel = new CarouselView
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
			carousel.SetBinding(CarouselView.ItemsSourceProperty, new Binding(nameof(Issue35643ViewModel.Items)));
			carousel.SetBinding(CarouselView.CurrentItemProperty, new Binding(nameof(Issue35643ViewModel.CurrentItem)));

			var updateButton = new Button
			{
				Text = "Replace item 2 and update CurrentItem",
				AutomationId = "UpdateButton"
			};
			updateButton.Clicked += (s, e) =>
			{
				_viewModel.Items[2] = "2b";
				_viewModel.CurrentItem = "2b";
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
					new RowDefinition { Height = 200 },
					new RowDefinition { Height = GridLength.Auto },
				}
			};

			var headerLabel = new Label { Text = "Current Item:" };
			Grid.SetRow(headerLabel, 0);
			Grid.SetRow(currentItemLabel, 1);
			Grid.SetRow(carousel, 2);
			Grid.SetRow(updateButton, 3);

			layout.Children.Add(headerLabel);
			layout.Children.Add(currentItemLabel);
			layout.Children.Add(carousel);
			layout.Children.Add(updateButton);

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
