using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MonoTanx.Core
{
    // The state of the settings page without any drawing: which tab is open, which
    // row is selected, how the list scrolls, and what changing a value does. Pure, so
    // it can be tested without a graphics device. It edits the GameSettings it is given.
    //
    // The selectable items are the rows of the open tab, then "Reset all to defaults",
    // then "Back". Up and Down move through them (and wrap), Left and Right change the
    // selected row's value.
    public sealed class SettingsPage
    {
        // How many rows are shown at once; a longer tab scrolls.
        public const int VisibleRows = 9;

        // A coarse change (Shift) is this many steps.
        public const int CoarseSteps = 10;

        public static readonly List<SettingGroup> Groups = Enum.GetValues(typeof(SettingGroup)).Cast<SettingGroup>().ToList();

        private IReadOnlyList<SettingDefinition> rows;
        private MenuSelection selection;

        public SettingsPage(GameSettings settings)
        {
            Settings = settings;
            OpenGroup(SettingGroup.Match);
        }

        public GameSettings Settings { get; }
        public SettingGroup Group { get; private set; }
        public IReadOnlyList<SettingDefinition> Rows => rows;

        // Row index at the top of the visible window.
        public int FirstVisible { get; private set; }

        // True once anything has been changed through the page.
        public bool Changed { get; private set; }

        public int SelectedIndex => selection.Index;
        public int ItemCount => selection.Count;
        public bool IsRowSelected => selection.Index < rows.Count;
        public bool IsResetAllSelected => selection.Index == rows.Count;
        public bool IsBackSelected => selection.Index == rows.Count + 1;
        public SettingDefinition SelectedRow => IsRowSelected ? rows[selection.Index] : null;
        public bool CanScrollUp => FirstVisible > 0;
        public bool CanScrollDown => FirstVisible + VisibleRows < rows.Count;

        public void NextGroup() => OpenGroup(Groups[(Groups.IndexOf(Group) + 1) % Groups.Count]);

        public void PreviousGroup() => OpenGroup(Groups[(Groups.IndexOf(Group) + Groups.Count - 1) % Groups.Count]);

        public void OpenGroup(SettingGroup group)
        {
            Group = group;
            rows = SettingsCatalogue.InGroup(group).ToList();
            selection = new MenuSelection(rows.Count + 2);
            FirstVisible = 0;
        }

        public void Next()
        {
            selection.Next();
            KeepSelectionVisible();
        }

        public void Previous()
        {
            selection.Previous();
            KeepSelectionVisible();
        }

        // Selects an item (a row of the open tab, or one of the two buttons after them).
        public void Select(int index)
        {
            selection.Select(index);
            KeepSelectionVisible();
        }

        // Moves the selected row's value one step (or ten) up (direction > 0) or down. Returns
        // whether the value changed. The result is kept on the step grid and within the range.
        public bool Adjust(int direction, bool coarse = false)
        {
            var row = SelectedRow;
            if (row == null || direction == 0)
                return false;

            var current = Settings.Get(row.Key);
            var stepped = current + Math.Sign(direction) * row.Step * (coarse ? CoarseSteps : 1);
            var snapped = (float)(Math.Round(stepped / row.Step) * row.Step);
            var stored = Settings.Set(row.Key, (float)Math.Round(snapped, 5));
            if (stored == current)
                return false;
            Changed = true;
            return true;
        }

        public void ResetSelected()
        {
            var row = SelectedRow;
            if (row == null || Settings.IsDefault(row.Key))
                return;
            Settings.Reset(row.Key);
            Changed = true;
        }

        public void ResetAll()
        {
            if (!Settings.Differences().Any())
                return;
            Settings.ResetAll();
            Changed = true;
        }

        // The value as shown: whole numbers plainly, others to as many places as their step needs.
        public static string Format(SettingDefinition definition, float value)
        {
            var places = definition.IsWhole ? 0 : Math.Min(3, Math.Max(0, DecimalPlaces(definition.Step)));
            return value.ToString("F" + places, CultureInfo.InvariantCulture);
        }

        private static int DecimalPlaces(float step)
        {
            var places = 0;
            var scaled = (double)step;
            while (places < 5 && Math.Abs(scaled - Math.Round(scaled)) > 1e-6)
            {
                scaled *= 10.0;
                places++;
            }
            return places;
        }

        private void KeepSelectionVisible()
        {
            if (!IsRowSelected)
                return;
            if (selection.Index < FirstVisible)
                FirstVisible = selection.Index;
            else if (selection.Index >= FirstVisible + VisibleRows)
                FirstVisible = selection.Index - VisibleRows + 1;
            FirstVisible = Math.Max(0, Math.Min(FirstVisible, Math.Max(0, rows.Count - VisibleRows)));
        }
    }
}
