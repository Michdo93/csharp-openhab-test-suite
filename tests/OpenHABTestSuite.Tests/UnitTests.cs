using OpenHABTestSuite;
using Xunit;

namespace OpenHABTestSuite.Tests;

public class UnitTests
{
    [Fact]
    public void ValueChecks()
    {
        Assert.True(ItemTester.IsValidSwitchValue(" on "));
        Assert.False(ItemTester.IsValidSwitchValue("TOGGLE"));
        Assert.True(ItemTester.IsValidContactValue("closed"));
        Assert.True(ItemTester.IsValidDimmerValue("50") && ItemTester.IsValidDimmerValue("0"));
        Assert.False(ItemTester.IsValidDimmerValue("101") || ItemTester.IsValidDimmerValue("viel"));
        Assert.True(ItemTester.IsValidRollershutterValue("MOVE") && ItemTester.IsValidRollershutterValue("100"));
        Assert.True(ItemTester.IsValidColorValue("240,100,100"));
        Assert.False(ItemTester.IsValidColorValue("red") || ItemTester.IsValidColorValue("400,1,1"));
        Assert.True(ItemTester.IsValidPlayerValue("FASTFORWARD"));
        Assert.True(ItemTester.IsValidNumberValue("42") && ItemTester.IsValidNumberValue("-20.5 °C"));
        Assert.False(ItemTester.IsValidNumberValue("20,5"));
        Assert.True(ItemTester.IsValidDateTimeValue("2024-01-15T08:30:00+0000"));
        Assert.True(ItemTester.IsValidDateTimeValue("2024-01-15T08:30:00.000Z"));
        Assert.False(ItemTester.IsValidDateTimeValue("15.01.2024"));
        Assert.True(ItemTester.IsValidLocationValue("48.7758,9.1829") && ItemTester.IsValidLocationValue("48.7758,9.1829,300.0"));
        Assert.False(ItemTester.IsValidLocationValue("91,0"));
        Assert.True(ItemTester.IsValidImageValue("https://example.org/a.png"));
        Assert.True(ItemTester.IsValidImageValue("data:image/svg+xml;base64,PHN2Zz4="));
        Assert.False(ItemTester.IsValidImageValue("bild.png"));
        Assert.False(ItemTester.IsValidSwitchValue(null));
    }

    [Fact]
    public void ExpectedStates()
    {
        ExpectedState one = "ON";
        ExpectedState two = new[] { "30", "30.0" };
        Assert.True(one.Matches("ON") && !one.Matches("OFF"));
        Assert.True(two.Matches("30.0") && !two.Matches("31"));
        Assert.Equal("'ON'", one.ToString());
        Assert.Equal("['30', '30.0']", two.ToString());
        ExpectedState? none = (string?)null;
        Assert.Null(none);
    }
}
