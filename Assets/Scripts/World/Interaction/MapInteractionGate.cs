namespace Institute.World
{
    /// <summary>
    /// Lets UI Toolkit panels suppress map picking while the pointer is over them.
    /// HUD controllers set <see cref="PointerOverUI"/> on pointer enter/leave of panels.
    /// </summary>
    public static class MapInteractionGate
    {
        public static bool PointerOverUI;
    }
}
