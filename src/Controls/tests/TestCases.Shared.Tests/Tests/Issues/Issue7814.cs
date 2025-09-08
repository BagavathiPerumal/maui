using NUnit.Framework;
using UITest.Appium;
using UITest.Core;
using System;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue7814 : _IssuesUITest
{
    public Issue7814(TestDevice testDevice) : base(testDevice)
    {
    }

    public override string Issue => "Vertical scrolling not working for CarouselView and CustomLayouts";

    [Test]
    [Category(UITestCategories.CarouselView)]
    [Category(UITestCategories.ScrollView)]
    public void VerticalScrollingShouldWorkAfterHorizontalScrolling()
    {
        App.WaitForElement("TestCarouselView");
        App.ScrollDown("TestCarouselView");
        App.WaitForElement("HorizontalScrollView");
        App.ScrollRight("HorizontalScrollView");
        App.WaitForElement("TestCarouselView");
        App.ScrollUp("TestCarouselView");
        VerifyScreenshot();
    }
}
