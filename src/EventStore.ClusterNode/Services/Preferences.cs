using System;

namespace EventStore.ClusterNode.Services;

public class Preferences {
    public void ToggleTheme() {
        SetTheme(!DarkMode);
    }

    public void SetTheme(bool darkMode) {
        DarkMode = darkMode;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool DarkMode { get; private set; } = true;

    public event EventHandler ThemeChanged;
}
