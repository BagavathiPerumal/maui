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
				CurrentItem = "2",
				// Section 2 uses its own independent collection so test 1 mutations don't affect it.
				NonCurrentItems = new ObservableCollection<string> { "0", "1", "2" },
				NonCurrentCurrentItem = "2",
				LoopItems = new ObservableCollection<string> { "A", "B", "C" },
				LoopCurrentItem = "C",
				// KeepLastItemInView test: CurrentItem at position 0; Replace last item must not move position.
				KeepLastItems = new ObservableCollection<string> { "P", "Q", "R" },
				KeepLastCurrentItem = "P",
				// Duplicate-value test: Items[0] and Items[2] are equal strings. Replace Items[2] must not
				// move the carousel off Items[0] — ensures Replace uses index comparison, not Equals().
				DupItems = new ObservableCollection<string> { "A", "B", "A" },
				DupCurrentItem = "A"
			};

			// ── Section 1: Replace current item (Loop=false) ────────────────────────
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

			var positionLabel = new Label
			{
				AutomationId = "PositionLabel",
				FontSize = 18,
				HorizontalTextAlignment = TextAlignment.Center
			};
			// Direct binding so PositionLabel always reflects the current Position, including at startup.
			positionLabel.SetBinding(Label.TextProperty, new Binding("Position", source: carousel));

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

			// ── Section 2: Replace non-current item (Loop=false) ───────────────────
			// Uses an independent collection (NonCurrentItems) so test 1's mutation of Items[2]
			// does not pollute this test's initial state. Both tests can run in the same session.
			var nonCurrentCurrentItemLabel = new Label
			{
				AutomationId = "NonCurrentCurrentItemLabel",
				FontSize = 24,
				HorizontalTextAlignment = TextAlignment.Center
			};
			nonCurrentCurrentItemLabel.SetBinding(Label.TextProperty, new Binding(nameof(Issue35643ViewModel.NonCurrentCurrentItem)));

			var nonCurrentCarousel = new CarouselView
			{
				Loop = false,
				AutomationId = "NonCurrentCarouselView",
				ItemTemplate = new DataTemplate(() =>
				{
					var lbl = new Label
					{
						HorizontalTextAlignment = TextAlignment.Center,
						VerticalTextAlignment = TextAlignment.Center,
						FontSize = 20
					};
					lbl.SetBinding(Label.TextProperty, ".");
					return new Frame { Content = lbl, BackgroundColor = Colors.LightSkyBlue };
				})
			};
			nonCurrentCarousel.SetBinding(CarouselView.ItemsSourceProperty, new Binding(nameof(Issue35643ViewModel.NonCurrentItems)));
			nonCurrentCarousel.SetBinding(CarouselView.CurrentItemProperty, new Binding(nameof(Issue35643ViewModel.NonCurrentCurrentItem)));

			var nonCurrentPositionLabel = new Label
			{
				AutomationId = "NonCurrentPositionLabel",
				FontSize = 18,
				HorizontalTextAlignment = TextAlignment.Center
			};
			nonCurrentPositionLabel.SetBinding(Label.TextProperty, new Binding("Position", source: nonCurrentCarousel));

			// Replaces NonCurrentItems[0] ("0" → "0b") while CurrentItem stays at NonCurrentItems[2].
			// Position must not change — Replace must not trigger KeepItemsInView reset.
			var replaceNonCurrentButton = new Button
			{
				Text = "Replace item 0 (non-current)",
				AutomationId = "ReplaceNonCurrentButton"
			};
			replaceNonCurrentButton.Clicked += (s, e) =>
			{
				_viewModel.NonCurrentItems[0] = "0b";
			};

			// ── Section 3: Replace current item (Loop=true) ────────────────────────
			// Validates that the loopable virtual adapter is rebuilt and position is preserved.
			// Initialize at "C" (position 2 = last item) to avoid a Mac/iOS timing race:
			// with Loop=true, UICollectionView first renders at Row 0 of the virtual adapter,
			// which maps to index ItemCount-1 = 2 ("C"). Initializing LoopCurrentItem="C"
			// means the initial render and the ViewModel value agree — no race condition.
			var loopCurrentItemLabel = new Label
			{
				AutomationId = "LoopCurrentItemLabel",
				FontSize = 24,
				HorizontalTextAlignment = TextAlignment.Center
			};
			loopCurrentItemLabel.SetBinding(Label.TextProperty, new Binding(nameof(Issue35643ViewModel.LoopCurrentItem)));

			var loopCarousel = new CarouselView
			{
				Loop = true,
				AutomationId = "LoopCarouselView",
				ItemTemplate = new DataTemplate(() =>
				{
					var label = new Label
					{
						HorizontalTextAlignment = TextAlignment.Center,
						VerticalTextAlignment = TextAlignment.Center,
						FontSize = 20
					};
					label.SetBinding(Label.TextProperty, ".");
					return new Frame { Content = label, BackgroundColor = Colors.LightCoral };
				})
			};
			loopCarousel.SetBinding(CarouselView.ItemsSourceProperty, new Binding(nameof(Issue35643ViewModel.LoopItems)));
			loopCarousel.SetBinding(CarouselView.CurrentItemProperty, new Binding(nameof(Issue35643ViewModel.LoopCurrentItem)));

			var loopPositionLabel = new Label
			{
				AutomationId = "LoopPositionLabel",
				FontSize = 18,
				HorizontalTextAlignment = TextAlignment.Center
			};
			loopPositionLabel.SetBinding(Label.TextProperty, new Binding("Position", source: loopCarousel));

			var loopReplaceButton = new Button
			{
				Text = "Replace current item in loop carousel",
				AutomationId = "LoopReplaceButton"
			};
			loopReplaceButton.Clicked += (s, e) =>
			{
				// Replace items[2] ("C" → "C2") — this is the current item (Position=2).
				// On Android with Loop=true, UpdateAdapter() must be called so the virtual
				// infinite adapter reflects the replacement; position must stay at 2.
				_viewModel.LoopItems[2] = "C2";
				_viewModel.LoopCurrentItem = "C2";
			};

			// ── Section 4: KeepLastItemInView — Replace last (non-current) item ────
			// When ItemsUpdatingScrollMode = KeepLastItemInView, Insert/Remove operations
			// scroll the carousel to the last item. A Replace must bypass this path entirely;
			// CurrentItem at position 0 must stay in view and Position must not jump to 2.
			var keepLastCurrentItemLabel = new Label
			{
				AutomationId = "KeepLastCurrentItemLabel",
				FontSize = 24,
				HorizontalTextAlignment = TextAlignment.Center
			};
			keepLastCurrentItemLabel.SetBinding(Label.TextProperty, new Binding(nameof(Issue35643ViewModel.KeepLastCurrentItem)));

			var keepLastCarousel = new CarouselView
			{
				Loop = false,
				ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepLastItemInView,
				AutomationId = "KeepLastCarouselView",
				ItemTemplate = new DataTemplate(() =>
				{
					var lbl = new Label
					{
						HorizontalTextAlignment = TextAlignment.Center,
						VerticalTextAlignment = TextAlignment.Center,
						FontSize = 20
					};
					lbl.SetBinding(Label.TextProperty, ".");
					return new Frame { Content = lbl, BackgroundColor = Colors.LightGreen };
				})
			};
			keepLastCarousel.SetBinding(CarouselView.ItemsSourceProperty, new Binding(nameof(Issue35643ViewModel.KeepLastItems)));
			keepLastCarousel.SetBinding(CarouselView.CurrentItemProperty, new Binding(nameof(Issue35643ViewModel.KeepLastCurrentItem)));

			var keepLastPositionLabel = new Label
			{
				AutomationId = "KeepLastPositionLabel",
				FontSize = 18,
				HorizontalTextAlignment = TextAlignment.Center
			};
			keepLastPositionLabel.SetBinding(Label.TextProperty, new Binding("Position", source: keepLastCarousel));

			var keepLastReplaceButton = new Button
			{
				Text = "Replace last item R → R2 (KeepLastItemInView)",
				AutomationId = "KeepLastReplaceButton"
			};
			keepLastReplaceButton.Clicked += (s, e) =>
			{
				// Replace items[2] — the last, non-current item. KeepLastItemInView must
				// NOT move the position to 2; a Replace does not change item count.
				_viewModel.KeepLastItems[2] = "R2";
			};

			// ── Section 5: Duplicate-value items — Replace must use index, not Equals ──
			// Items[0] and Items[2] are both "A". GetPosition(CurrentItem) returns 0 for both.
			// Old Equals-based lookup would wrongly treat Replace of items[2] as a current-item
			// Replace. The fix compares e.OldStartingIndex to carouselPosition instead.
			var dupCurrentItemLabel = new Label
			{
				AutomationId = "DupCurrentItemLabel",
				FontSize = 24,
				HorizontalTextAlignment = TextAlignment.Center
			};
			dupCurrentItemLabel.SetBinding(Label.TextProperty, new Binding(nameof(Issue35643ViewModel.DupCurrentItem)));

			var dupCarousel = new CarouselView
			{
				Loop = false,
				AutomationId = "DupCarouselView",
				ItemTemplate = new DataTemplate(() =>
				{
					var lbl = new Label
					{
						HorizontalTextAlignment = TextAlignment.Center,
						VerticalTextAlignment = TextAlignment.Center,
						FontSize = 20
					};
					lbl.SetBinding(Label.TextProperty, ".");
					return new Frame { Content = lbl, BackgroundColor = Colors.LightYellow };
				})
			};
			dupCarousel.SetBinding(CarouselView.ItemsSourceProperty, new Binding(nameof(Issue35643ViewModel.DupItems)));
			dupCarousel.SetBinding(CarouselView.CurrentItemProperty, new Binding(nameof(Issue35643ViewModel.DupCurrentItem)));

			var dupPositionLabel = new Label
			{
				AutomationId = "DupPositionLabel",
				FontSize = 18,
				HorizontalTextAlignment = TextAlignment.Center
			};
			dupPositionLabel.SetBinding(Label.TextProperty, new Binding("Position", source: dupCarousel));

			var dupReplaceButton = new Button
			{
				Text = "Replace items[2] (duplicate 'A' → 'X')",
				AutomationId = "DupReplaceButton"
			};
			dupReplaceButton.Clicked += (s, e) =>
			{
				// Replace items[2] which has the same string value "A" as items[0] (CurrentItem).
				// Equals-based detection would mistake this for a current-item Replace and
				// incorrectly move the carousel. Index-based detection must keep Position at 0.
				_viewModel.DupItems[2] = "X";
			};

			BindingContext = _viewModel;

			var scrollView = new ScrollView
			{
				Content = new VerticalStackLayout
				{
					Padding = new Thickness(10),
					Spacing = 10,
					Children =
					{
						new Label { Text = "── Loop=false: Replace current item ──", FontAttributes = FontAttributes.Bold },
						new Label { Text = "Current Item:" },
						currentItemLabel,
						positionLabel,
						new ContentView { Content = carousel, HeightRequest = 200 },
						updateButton,
						new Label { Text = "── Loop=false: Replace non-current item ──", FontAttributes = FontAttributes.Bold },
						new Label { Text = "NonCurrent Item:" },
						nonCurrentCurrentItemLabel,
						nonCurrentPositionLabel,
						new ContentView { Content = nonCurrentCarousel, HeightRequest = 200 },
						replaceNonCurrentButton,
						new Label { Text = "── Loop=true: Replace current item ──", FontAttributes = FontAttributes.Bold },
						new Label { Text = "Loop Current Item:" },
						loopCurrentItemLabel,
						loopPositionLabel,
						new ContentView { Content = loopCarousel, HeightRequest = 200 },
						loopReplaceButton,
						new Label { Text = "── KeepLastItemInView: Replace last item ──", FontAttributes = FontAttributes.Bold },
						new Label { Text = "KeepLast Current Item:" },
						keepLastCurrentItemLabel,
						keepLastPositionLabel,
						new ContentView { Content = keepLastCarousel, HeightRequest = 200 },
						keepLastReplaceButton,
						new Label { Text = "── Duplicate values: Replace non-current duplicate ──", FontAttributes = FontAttributes.Bold },
						new Label { Text = "Dup Current Item:" },
						dupCurrentItemLabel,
						dupPositionLabel,
						new ContentView { Content = dupCarousel, HeightRequest = 200 },
						dupReplaceButton,
					}
				}
			};

			Content = scrollView;
		}
	}

	public class Issue35643ViewModel : INotifyPropertyChanged
	{
		ObservableCollection<string> _items;
		string _currentItem;
		ObservableCollection<string> _nonCurrentItems;
		string _nonCurrentCurrentItem;
		ObservableCollection<string> _loopItems;
		string _loopCurrentItem;
		ObservableCollection<string> _keepLastItems;
		string _keepLastCurrentItem;
		ObservableCollection<string> _dupItems;
		string _dupCurrentItem;

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

		public ObservableCollection<string> NonCurrentItems
		{
			get => _nonCurrentItems;
			set => SetProperty(ref _nonCurrentItems, value);
		}

		public string NonCurrentCurrentItem
		{
			get => _nonCurrentCurrentItem;
			set => SetProperty(ref _nonCurrentCurrentItem, value);
		}

		public ObservableCollection<string> LoopItems
		{
			get => _loopItems;
			set => SetProperty(ref _loopItems, value);
		}

		public string LoopCurrentItem
		{
			get => _loopCurrentItem;
			set => SetProperty(ref _loopCurrentItem, value);
		}

		public ObservableCollection<string> KeepLastItems
		{
			get => _keepLastItems;
			set => SetProperty(ref _keepLastItems, value);
		}

		public string KeepLastCurrentItem
		{
			get => _keepLastCurrentItem;
			set => SetProperty(ref _keepLastCurrentItem, value);
		}

		public ObservableCollection<string> DupItems
		{
			get => _dupItems;
			set => SetProperty(ref _dupItems, value);
		}

		public string DupCurrentItem
		{
			get => _dupCurrentItem;
			set => SetProperty(ref _dupCurrentItem, value);
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
