using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls.Internals;
using Xunit;

namespace Microsoft.Maui.Controls.Xaml.UnitTests;

// Page-level ViewModel — this is what the RelativeSource AncestorType points to
public class Maui34056PageViewModel
{
    public ObservableCollection<Maui34056ItemViewModel> Items { get; } =
        [new Maui34056ItemViewModel { ItemName = "Item 1" }];

    public ICommand TestCommand { get; } = new Command(() => { });
}

// Item ViewModel — this is what the DataTemplate's x:DataType is set to
public class Maui34056ItemViewModel
{
    public string ItemName { get; set; } = "";
}

public partial class Maui34056 : ContentPage
{
    public Maui34056()
    {
        InitializeComponent();
        BindingContext = new Maui34056PageViewModel();
    }

    [Collection("Issue")]
    public class Maui34056Tests
    {
        [Theory]
        [XamlInflatorData]
        internal void RelativeSourceAncestorTypeInDataTemplateGeneratesCompiledBinding(XamlInflator inflator)
        {
            var page = new Maui34056(inflator);

            var template = ((CollectionView)page.TestCollectionView).ItemTemplate;
            var content = template.CreateContent() as Button;
            Assert.NotNull(content);

            var binding = content.GetContext(Button.CommandProperty)?.Bindings.GetValue();

            if (inflator is XamlInflator.Runtime)
            {
                // Runtime inflator uses the string-based Binding — no compile-time type info available.
                Assert.IsType<Binding>(binding);
            }
            else
            {
                // Both XamlC and SourceGen produce a trim-safe TypedBinding when x:DataType is present
                // on the binding node alongside RelativeSource AncestorType.
                // - XamlC: compiles using the explicit x:DataType on the binding node.
                // - SourceGen (the bug fix): infers source type from AncestorType via context.Types lookup.
                //   Previously SourceGen always fell back to string-based Binding for RelativeSource,
                //   which was trimmed under AOT/Release.
                Assert.IsType<TypedBinding<Maui34056PageViewModel, ICommand>>(binding);
            }
        }
    }
}
