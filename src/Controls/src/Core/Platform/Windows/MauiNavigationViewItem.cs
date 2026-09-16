#nullable disable
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.Maui.Controls.Platform;

public partial class MauiNavigationViewItem : NavigationViewItem
{
	public MauiNavigationViewItem()
	{
		RegisterPropertyChangedCallback(MenuItemsSourceProperty, OnMenuItemsSourceChanged);
		UpdateSelectsOnInvoked();
	}

	void OnMenuItemsSourceChanged(DependencyObject sender, DependencyProperty dp) => UpdateSelectsOnInvoked();

	void UpdateSelectsOnInvoked()
	{
		var newValue = MenuItemsSource is null;
		SelectsOnInvoked = newValue;
	}
}
