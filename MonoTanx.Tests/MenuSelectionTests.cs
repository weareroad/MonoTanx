using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class MenuSelectionTests
{
    [Fact]
    public void StartsOnTheFirstItem()
    {
        Assert.Equal(0, new MenuSelection(4).Index);
    }

    [Fact]
    public void NextMovesDownAndWrapsToTheTop()
    {
        var selection = new MenuSelection(3);

        selection.Next();
        Assert.Equal(1, selection.Index);
        selection.Next();
        Assert.Equal(2, selection.Index);
        selection.Next();
        Assert.Equal(0, selection.Index);
    }

    [Fact]
    public void PreviousMovesUpAndWrapsToTheBottom()
    {
        var selection = new MenuSelection(3);

        selection.Previous();
        Assert.Equal(2, selection.Index);
        selection.Previous();
        Assert.Equal(1, selection.Index);
    }

    [Fact]
    public void SelectJumpsToAValidItemAndIgnoresInvalidOnes()
    {
        var selection = new MenuSelection(4);

        selection.Select(2);
        Assert.Equal(2, selection.Index);

        selection.Select(-1);
        selection.Select(4);
        selection.Select(99);
        Assert.Equal(2, selection.Index);
    }

    [Fact]
    public void CanStartOnAGivenItem()
    {
        Assert.Equal(1, new MenuSelection(3, 1).Index);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void RejectsAMenuWithNoItems(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MenuSelection(count));
    }

    [Fact]
    public void ASingleItemMenuStaysPut()
    {
        var selection = new MenuSelection(1);

        selection.Next();
        selection.Previous();

        Assert.Equal(0, selection.Index);
    }
}
