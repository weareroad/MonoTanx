using System;

namespace MonoTanx.Core
{
    // Which item of a vertical menu is highlighted. Moving past either end wraps
    // around. Pure state, so it can be tested without a graphics device.
    public sealed class MenuSelection
    {
        public int Count { get; }
        public int Index { get; private set; }

        public MenuSelection(int count, int index = 0)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "A menu needs at least one item.");
            Count = count;
            Select(index);
        }

        public void Next() => Index = (Index + 1) % Count;

        public void Previous() => Index = (Index + Count - 1) % Count;

        // Out-of-range requests are ignored.
        public void Select(int index)
        {
            if (index >= 0 && index < Count)
                Index = index;
        }
    }
}
