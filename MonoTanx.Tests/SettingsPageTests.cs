using MonoTanx.Core;
using System.Linq;
using Xunit;

namespace MonoTanx.Tests;

public class SettingsPageTests
{
    private static void Select(SettingsPage page, string key)
    {
        page.OpenGroup(SettingsCatalogue.Find(key).Group);
        page.Select(page.Rows.ToList().FindIndex(row => row.Key == key));
    }

    [Fact]
    public void StartsOnTheFirstRowOfTheMatchTab()
    {
        var page = new SettingsPage(new GameSettings());

        Assert.Equal(SettingGroup.Match, page.Group);
        Assert.Equal(0, page.SelectedIndex);
        Assert.True(page.IsRowSelected);
        Assert.Equal(SettingsCatalogue.InGroup(SettingGroup.Match).Count() + 2, page.ItemCount);
    }

    [Fact]
    public void TabsWrapAndEachShowsItsOwnRows()
    {
        var page = new SettingsPage(new GameSettings());

        page.PreviousGroup();
        Assert.Equal(SettingGroup.PerSeat, page.Group);
        page.NextGroup();
        Assert.Equal(SettingGroup.Match, page.Group);
        foreach (var group in SettingsPage.Groups)
        {
            page.OpenGroup(group);
            Assert.NotEmpty(page.Rows);
            Assert.All(page.Rows, row => Assert.Equal(group, row.Group));
        }
        Assert.Equal(SettingsCatalogue.All.Count, SettingsPage.Groups.Sum(group => SettingsCatalogue.InGroup(group).Count()));
    }

    [Fact]
    public void SelectionWrapsThroughTheRowsThenResetAllAndBack()
    {
        var page = new SettingsPage(new GameSettings());
        page.OpenGroup(SettingGroup.Match);
        var rows = page.Rows.Count;

        page.Previous();
        Assert.True(page.IsBackSelected);
        page.Previous();
        Assert.True(page.IsResetAllSelected);
        page.Previous();
        Assert.Equal(rows - 1, page.SelectedIndex);
        page.Select(rows);
        page.Next();
        page.Next();
        Assert.Equal(0, page.SelectedIndex);
    }

    [Fact]
    public void LeftAndRightMoveOneStepAndShiftMovesTen()
    {
        var settings = new GameSettings();
        var page = new SettingsPage(settings);
        Select(page, SettingKeys.ForwardSpeed);

        Assert.True(page.Adjust(1));
        Assert.Equal(95.0f, settings.Get(SettingKeys.ForwardSpeed));
        Assert.True(page.Adjust(1, coarse: true));
        Assert.Equal(145.0f, settings.Get(SettingKeys.ForwardSpeed));
        Assert.True(page.Adjust(-1));
        Assert.Equal(140.0f, settings.Get(SettingKeys.ForwardSpeed));
        Assert.True(page.Changed);
    }

    [Fact]
    public void SmallStepsDoNotDriftBecauseOfFloatingPoint()
    {
        var settings = new GameSettings();
        var page = new SettingsPage(settings);
        Select(page, SettingKeys.AiSkill);

        for (var i = 0; i < 3; i++)
            page.Adjust(-1);

        Assert.Equal(0.35f, settings.Get(SettingKeys.AiSkill));
    }

    [Fact]
    public void ChangesStopAtTheEndsOfTheRangeAndSayNothingChanged()
    {
        var settings = new GameSettings();
        var page = new SettingsPage(settings);
        Select(page, SettingKeys.AiSkill);

        page.Adjust(1, coarse: true);
        Assert.Equal(1.0f, settings.Get(SettingKeys.AiSkill));
        Assert.False(page.Adjust(1));
    }

    [Fact]
    public void WholeNumbersStayWhole()
    {
        var settings = new GameSettings();
        var page = new SettingsPage(settings);
        Select(page, SettingKeys.Damage);

        page.Adjust(1);

        Assert.Equal(Tuning.StandardShell.Damage + 1, settings.GetWhole(SettingKeys.Damage));
    }

    [Fact]
    public void NothingChangesWhenAButtonIsSelected()
    {
        var settings = new GameSettings();
        var page = new SettingsPage(settings);
        page.Previous();

        Assert.False(page.Adjust(1));
        Assert.Empty(settings.Differences());
        Assert.False(page.Changed);
    }

    [Fact]
    public void ResetSelectedAndResetAll()
    {
        var settings = new GameSettings();
        settings.Set(SettingKeys.Damage, 40);
        settings.Set(SettingKeys.AiSkill, 0.9f);
        var page = new SettingsPage(settings);
        Select(page, SettingKeys.Damage);

        page.ResetSelected();
        Assert.True(settings.IsDefault(SettingKeys.Damage));
        Assert.False(settings.IsDefault(SettingKeys.AiSkill));

        page.ResetAll();
        Assert.Empty(settings.Differences());
        Assert.True(page.Changed);
    }

    [Fact]
    public void ResettingWhatIsAlreadyTheDefaultIsNotAChange()
    {
        var page = new SettingsPage(new GameSettings());

        page.ResetSelected();
        page.ResetAll();

        Assert.False(page.Changed);
    }

    [Fact]
    public void ALongTabScrollsToKeepTheSelectionInView()
    {
        var page = new SettingsPage(new GameSettings());
        page.OpenGroup(SettingGroup.Computer);
        var count = page.Rows.Count;
        Assert.True(count > SettingsPage.VisibleRows);
        Assert.False(page.CanScrollUp);
        Assert.True(page.CanScrollDown);

        for (var i = 0; i < SettingsPage.VisibleRows; i++)
            page.Next();
        Assert.Equal(1, page.FirstVisible);

        page.Previous();
        page.Previous();
        Assert.Equal(1, page.FirstVisible);
        page.Select(0);
        Assert.Equal(0, page.FirstVisible);

        page.Previous(); // wraps to Back; the window stays
        page.Previous();
        page.Previous(); // the last row
        Assert.Equal(count - SettingsPage.VisibleRows, page.FirstVisible);
        Assert.False(page.CanScrollDown);
        Assert.True(page.CanScrollUp);
    }

    [Fact]
    public void ARowCanBeSeenWheneverItIsSelected()
    {
        var page = new SettingsPage(new GameSettings());
        foreach (var group in SettingsPage.Groups)
        {
            page.OpenGroup(group);
            for (var i = 0; i < page.ItemCount; i++)
            {
                page.Next();
                if (page.IsRowSelected)
                    Assert.InRange(page.SelectedIndex, page.FirstVisible, page.FirstVisible + SettingsPage.VisibleRows - 1);
            }
        }
    }

    [Theory]
    [InlineData(SettingKeys.Damage, 12.0f, "12")]
    [InlineData(SettingKeys.AiSkill, 0.5f, "0.50")]
    [InlineData(SettingKeys.ForwardSpeed, 90.0f, "90")]
    [InlineData(SettingKeys.TurnSpeed, 2.5f, "2.5")]
    [InlineData(SettingKeys.TurnFuelPerSecond, 0.25f, "0.25")]
    [InlineData(SettingKeys.AiAimToleranceRadians, 0.07f, "0.07")]
    public void ValuesAreShownWithAsManyPlacesAsTheirStepNeeds(string key, float value, string expected)
    {
        Assert.Equal(expected, SettingsPage.Format(SettingsCatalogue.Find(key), value));
    }

    [Fact]
    public void EveryDefaultCanBeShown()
    {
        foreach (var definition in SettingsCatalogue.All)
            Assert.False(string.IsNullOrEmpty(SettingsPage.Format(definition, definition.Default)));
    }
}
