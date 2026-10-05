namespace MonoTanx.Core
{
    // Tells whether the mouse has moved since it was last asked. A menu lets the mouse
    // move the highlight, but only when the mouse actually moves: a pointer that is
    // simply resting over an item must not pull the highlight back every frame, or the
    // keyboard could never move it. Pure, so it can be tested without a mouse.
    public sealed class MouseMovement
    {
        private int x;
        private int y;
        private bool known;

        // Records the pointer position; true if it differs from the last one given. The
        // first position is only remembered (nothing has moved yet).
        public bool Moved(int newX, int newY)
        {
            var moved = known && (newX != x || newY != y);
            x = newX;
            y = newY;
            known = true;
            return moved;
        }
    }
}
