namespace SystemRegisIII.WaylandForge.Ui;

public readonly record struct UiFilePickerResult(bool IsOpen, bool Accepted, bool Cancelled, string? SelectedPath);

public sealed class UiFilePicker
{
    private readonly UiId _pathBoxId = new("filepicker.path");
    private string _currentDirectory;
    private string? _selectedPath;
    private string? _error;
    private FileEntry[] _entries = [];

    public UiFilePicker()
    {
        _currentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(_currentDirectory) || !Directory.Exists(_currentDirectory))
        {
            _currentDirectory = Directory.GetCurrentDirectory();
        }
    }

    public string? SelectedPath => _selectedPath;

    public UiFilePickerResult Draw(UiContext ui, RectI rect, string title = "FILE PICKER")
    {
        EnsureEntries();

        RectI content = ui.Panel(rect, title);
        var top = new UiRow(content.X, content.Y, 18, 5);
        top = top.Next(30, out RectI upRect);
        if (ui.Button(new UiId("filepicker.up"), upRect, "UP").Clicked)
        {
            NavigateTo(Directory.GetParent(_currentDirectory)?.FullName ?? _currentDirectory, ui);
        }

        top = top.Next(42, out RectI homeRect);
        if (ui.Button(new UiId("filepicker.home"), homeRect, "HOME").Clicked)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            NavigateTo(string.IsNullOrWhiteSpace(home) ? _currentDirectory : home, ui);
        }

        top = top.Next(Math.Max(80, content.Width - 82), out RectI pathRect);
        UiTextBoxResult pathBox = ui.TextBox(_pathBoxId, pathRect, _currentDirectory, "path", new UiTextBoxOptions(MaxLength: 192));
        if (pathBox.Submitted)
        {
            NavigateTo(pathBox.Text, ui);
        }

        int listY = content.Y + 25;
        int listHeight = Math.Max(50, content.Height - 72);
        RectI listRect = new(content.X, listY, content.Width, listHeight);
        using (UiScrollArea scroll = ui.BeginScrollArea(new UiId("filepicker.scroll"), listRect, Math.Max(listHeight, _entries.Length * 20 + 4)))
        {
            int y = scroll.Content.Y + 2;
            foreach (FileEntry entry in _entries)
            {
                RectI row = new(scroll.Content.X + 2, y, scroll.Content.Width - 4, 17);
                bool active = string.Equals(_selectedPath, entry.Path, StringComparison.Ordinal);
                string prefix = entry.IsDirectory ? "[D]" : "[F]";
                if (ui.Button(new UiId("filepicker.entry:" + entry.Path), row, $"{prefix} {Truncate(entry.Name, Math.Max(8, row.Width / 6 - 4))}", active).Clicked)
                {
                    if (entry.IsDirectory)
                    {
                        NavigateTo(entry.Path, ui);
                    }
                    else
                    {
                        _selectedPath = entry.Path;
                    }
                }
                y += 20;
            }
        }

        int footerY = content.Bottom - 38;
        if (!string.IsNullOrEmpty(_selectedPath))
        {
            ui.Text(content.X, footerY, Truncate(_selectedPath, Math.Max(10, content.Width / 6)), UiTextKind.Muted);
        }
        else if (!string.IsNullOrEmpty(_error))
        {
            ui.Text(content.X, footerY, Truncate(_error, Math.Max(10, content.Width / 6)), UiTextKind.Accent);
        }

        var footer = new UiRow(content.X, content.Bottom - 18, 18, 6);
        footer = footer.Next(60, out RectI openRect);
        bool accepted = ui.Button(new UiId("filepicker.open"), openRect, "OPEN", _selectedPath is not null).Clicked && _selectedPath is not null;
        footer = footer.Next(66, out RectI cancelRect);
        bool cancelled = ui.Button(new UiId("filepicker.cancel"), cancelRect, "CANCEL").Clicked;

        return new UiFilePickerResult(!accepted && !cancelled, accepted, cancelled, _selectedPath);
    }

    private void NavigateTo(string path, UiContext ui)
    {
        try
        {
            string fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
            if (File.Exists(fullPath))
            {
                _selectedPath = fullPath;
                _error = null;
                return;
            }

            if (!Directory.Exists(fullPath))
            {
                _error = "missing path";
                return;
            }

            _currentDirectory = fullPath;
            _selectedPath = null;
            _entries = [];
            _error = null;
            ui.SetText(_pathBoxId, _currentDirectory);
            EnsureEntries();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _error = ex.Message;
        }
    }

    private void EnsureEntries()
    {
        if (_entries.Length > 0)
        {
            return;
        }

        try
        {
            var entries = new List<FileEntry>();
            foreach (string directory in Directory.EnumerateDirectories(_currentDirectory).Order(StringComparer.OrdinalIgnoreCase))
            {
                entries.Add(new FileEntry(Path.GetFileName(directory), directory, true));
            }

            foreach (string file in Directory.EnumerateFiles(_currentDirectory).Order(StringComparer.OrdinalIgnoreCase))
            {
                entries.Add(new FileEntry(Path.GetFileName(file), file, false));
            }

            _entries = entries.ToArray();
            _error = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _entries = [];
            _error = ex.Message;
        }
    }

    private static string Truncate(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return text;
        }

        return maxChars <= 3 ? text[..maxChars] : text[..(maxChars - 3)] + "...";
    }

    private readonly record struct FileEntry(string Name, string Path, bool IsDirectory);
}
