namespace SystemRegisIII.WaylandForge.Ui;

public readonly record struct UiFilePickerResult(bool IsOpen, bool Accepted, bool Cancelled, string? SelectedPath);

public sealed class UiFilePicker
{
    private readonly UiId _pathBoxId = new("filepicker.path");
    private readonly UiId _filterBoxId = new("filepicker.filter");
    private string _currentDirectory;
    private string? _selectedPath;
    private string? _error;
    private DirectoryEntry[] _directories = [];
    private FileEntry[] _files = [];
    private bool _entriesLoaded;
    private string _filterText = string.Empty;
    private bool _romOnly;
    private FileSortMode _sortMode = FileSortMode.Name;
    private bool _sortDescending;

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

        int filterY = content.Y + 25;
        var filterRow = new UiRow(content.X, filterY, 18, 5);
        filterRow = filterRow.Next(58, out RectI filterLabelRect);
        ui.Text(filterLabelRect.X, filterLabelRect.Y + 5, "FILTER", UiTextKind.Muted);
        filterRow = filterRow.Next(Math.Max(90, content.Width - 184), out RectI filterRect);
        UiTextBoxResult filterBox = ui.TextBox(_filterBoxId, filterRect, _filterText, "name or ext", new UiTextBoxOptions(MaxLength: 64));
        if (filterBox.Changed)
        {
            _filterText = filterBox.Text;
            _selectedPath = null;
        }
        filterRow = filterRow.Next(64, out RectI romFilterRect);
        if (ui.ToggleButton(new UiId("filepicker.filter.rom"), romFilterRect, "ROM", _romOnly))
        {
            _romOnly = !_romOnly;
            _selectedPath = null;
        }

        int bodyY = content.Y + 49;
        int bodyHeight = Math.Max(58, content.Height - 102);
        int sideWidth = Math.Min(190, Math.Max(132, content.Width / 3));
        RectI sideRect = new(content.X, bodyY, sideWidth, bodyHeight);
        RectI fileRect = new(sideRect.Right + 8, bodyY, Math.Max(80, content.Right - sideRect.Right - 8), bodyHeight);

        DrawDirectoryPane(ui, sideRect);
        DrawFilePane(ui, fileRect);

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

    private void DrawDirectoryPane(UiContext ui, RectI rect)
    {
        RectI content = ui.Panel(rect, "FOLDERS");
        int rowHeight = 19;
        int fixedRows = 4;
        int listHeight = Math.Max(content.Height, (fixedRows + _directories.Length) * rowHeight + 4);
        using UiScrollArea scroll = ui.BeginScrollArea(new UiId("filepicker.folders.scroll"), content, listHeight);

        int y = scroll.Content.Y + 2;
        DrawPlaceButton(ui, "filepicker.place.home", new RectI(scroll.Content.X + 2, y, scroll.Content.Width - 4, 17), "HOME", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        y += rowHeight;
        DrawPlaceButton(ui, "filepicker.place.cwd", new RectI(scroll.Content.X + 2, y, scroll.Content.Width - 4, 17), "APP CWD", Directory.GetCurrentDirectory());
        y += rowHeight;
        DrawPlaceButton(ui, "filepicker.place.root", new RectI(scroll.Content.X + 2, y, scroll.Content.Width - 4, 17), "ROOT", Path.GetPathRoot(_currentDirectory) ?? "/");
        y += rowHeight;
        string parent = Directory.GetParent(_currentDirectory)?.FullName ?? _currentDirectory;
        DrawPlaceButton(ui, "filepicker.place.up", new RectI(scroll.Content.X + 2, y, scroll.Content.Width - 4, 17), "..", parent);
        y += rowHeight + 4;

        foreach (DirectoryEntry entry in _directories)
        {
            RectI row = new(scroll.Content.X + 2, y, scroll.Content.Width - 4, 17);
            bool active = string.Equals(_currentDirectory, entry.Path, StringComparison.Ordinal);
            string label = "[DIR] " + Truncate(entry.Name, Math.Max(8, row.Width / 6 - 6));
            if (ui.Button(new UiId("filepicker.dir:" + entry.Path), row, label, active).Clicked)
            {
                NavigateTo(entry.Path, ui);
            }
            y += rowHeight;
        }
    }

    private void DrawFilePane(UiContext ui, RectI rect)
    {
        RectI content = ui.Panel(rect, "FILES");
        int sortY = content.Y;
        var sort = new UiRow(content.X, sortY, 18, 5);
        sort = sort.Next(60, out RectI nameRect);
        if (ui.Button(new UiId("filepicker.sort.name"), nameRect, SortLabel("NAME", FileSortMode.Name), _sortMode == FileSortMode.Name).Clicked)
        {
            SetSort(FileSortMode.Name);
        }

        sort = sort.Next(56, out RectI typeRect);
        if (ui.Button(new UiId("filepicker.sort.type"), typeRect, SortLabel("EXT", FileSortMode.Type), _sortMode == FileSortMode.Type).Clicked)
        {
            SetSort(FileSortMode.Type);
        }

        sort = sort.Next(58, out RectI sizeRect);
        if (ui.Button(new UiId("filepicker.sort.size"), sizeRect, SortLabel("SIZE", FileSortMode.Size), _sortMode == FileSortMode.Size).Clicked)
        {
            SetSort(FileSortMode.Size);
        }

        sort = sort.Next(62, out RectI dateRect);
        if (ui.Button(new UiId("filepicker.sort.date"), dateRect, SortLabel("DATE", FileSortMode.Modified), _sortMode == FileSortMode.Modified).Clicked)
        {
            SetSort(FileSortMode.Modified);
        }

        RectI listRect = new(content.X, content.Y + 24, content.Width, Math.Max(20, content.Height - 24));
        FileEntry[] visibleFiles = FilterFiles().ToArray();
        int rowHeight = 20;
        int listHeight = Math.Max(listRect.Height, visibleFiles.Length * rowHeight + 4);
        using UiScrollArea scroll = ui.BeginScrollArea(new UiId("filepicker.files.scroll"), listRect, listHeight);

        if (visibleFiles.Length == 0)
        {
            ui.Text(scroll.Content.X + 4, scroll.Content.Y + 4, "NO FILES", UiTextKind.Muted);
            return;
        }

        int y = scroll.Content.Y + 2;
        foreach (FileEntry entry in visibleFiles)
        {
            RectI row = new(scroll.Content.X + 2, y, scroll.Content.Width - 4, 17);
            bool active = string.Equals(_selectedPath, entry.Path, StringComparison.Ordinal);
            string label = FormatFileLabel(entry, Math.Max(8, row.Width / 6 - 2));
            if (ui.Button(new UiId("filepicker.file:" + entry.Path), row, label, active).Clicked)
            {
                _selectedPath = entry.Path;
            }
            y += rowHeight;
        }
    }

    private void DrawPlaceButton(UiContext ui, string id, RectI rect, string label, string path)
    {
        if (ui.Button(new UiId(id), rect, Truncate(label, Math.Max(4, rect.Width / 6 - 1)), string.Equals(_currentDirectory, path, StringComparison.Ordinal)).Clicked)
        {
            NavigateTo(path, ui);
        }
    }

    private void SetSort(FileSortMode mode)
    {
        if (_sortMode == mode)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _sortMode = mode;
            _sortDescending = mode == FileSortMode.Modified;
        }

        _files = SortFiles(_files).ToArray();
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
            _directories = [];
            _files = [];
            _entriesLoaded = false;
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
        if (_entriesLoaded)
        {
            return;
        }

        try
        {
            var directories = new List<DirectoryEntry>();
            foreach (string directory in Directory.EnumerateDirectories(_currentDirectory).Order(StringComparer.OrdinalIgnoreCase))
            {
                directories.Add(new DirectoryEntry(Path.GetFileName(directory), directory));
            }

            var files = new List<FileEntry>();
            foreach (string file in Directory.EnumerateFiles(_currentDirectory).Order(StringComparer.OrdinalIgnoreCase))
            {
                var info = new FileInfo(file);
                files.Add(new FileEntry(info.Name, file, info.Extension, info.Length, info.LastWriteTime));
            }

            _directories = directories.ToArray();
            _files = SortFiles(files).ToArray();
            _entriesLoaded = true;
            _error = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _directories = [];
            _files = [];
            _entriesLoaded = true;
            _error = ex.Message;
        }
    }

    private IEnumerable<FileEntry> FilterFiles()
    {
        IEnumerable<FileEntry> files = _files;
        if (_romOnly)
        {
            files = files.Where(static entry => IsRomExtension(entry.Extension));
        }

        string filter = _filterText.Trim();
        if (filter.Length > 0)
        {
            files = files.Where(entry => entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) || entry.Extension.TrimStart('.').Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        return files;
    }

    private IEnumerable<FileEntry> SortFiles(IEnumerable<FileEntry> files)
    {
        IOrderedEnumerable<FileEntry> ordered = _sortMode switch
        {
            FileSortMode.Type => _sortDescending
                ? files.OrderByDescending(static entry => entry.Extension, StringComparer.OrdinalIgnoreCase).ThenByDescending(static entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                : files.OrderBy(static entry => entry.Extension, StringComparer.OrdinalIgnoreCase).ThenBy(static entry => entry.Name, StringComparer.OrdinalIgnoreCase),
            FileSortMode.Modified => _sortDescending
                ? files.OrderByDescending(static entry => entry.Modified).ThenBy(static entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                : files.OrderBy(static entry => entry.Modified).ThenBy(static entry => entry.Name, StringComparer.OrdinalIgnoreCase),
            FileSortMode.Size => _sortDescending
                ? files.OrderByDescending(static entry => entry.Size).ThenBy(static entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                : files.OrderBy(static entry => entry.Size).ThenBy(static entry => entry.Name, StringComparer.OrdinalIgnoreCase),
            _ => _sortDescending
                ? files.OrderByDescending(static entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                : files.OrderBy(static entry => entry.Name, StringComparer.OrdinalIgnoreCase),
        };
        return ordered;
    }

    private string SortLabel(string label, FileSortMode mode)
    {
        if (_sortMode != mode)
        {
            return label;
        }

        return _sortDescending ? label + " v" : label + " ^";
    }

    private static string FormatFileLabel(FileEntry entry, int maxChars)
    {
        string icon = FileIcon(entry.Extension);
        string extension = string.IsNullOrEmpty(entry.Extension) ? "FILE" : entry.Extension.TrimStart('.').ToUpperInvariant();
        string size = FormatSize(entry.Size);
        string date = entry.Modified.ToString("yyyy-MM-dd");
        string suffix = $"  {extension,-5} {size,8} {date}";
        int nameChars = Math.Max(6, maxChars - icon.Length - suffix.Length - 1);
        return $"{icon} {Truncate(entry.Name, nameChars)}{suffix}";
    }

    private static string FileIcon(string extension)
    {
        return extension.Trim().ToLowerInvariant() switch
        {
            _ when IsRomExtension(extension) => "[ROM]",
            ".zip" or ".7z" or ".rar" => "[ZIP]",
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".ppm" => "[IMG]",
            ".txt" or ".log" => "[TXT]",
            ".json" or ".toml" or ".cfg" or ".conf" or ".ini" => "[CFG]",
            ".so" or ".dll" or ".exe" => "[BIN]",
            _ => "[FILE]",
        };
    }

    private static bool IsRomExtension(string extension)
    {
        return extension.Trim().ToLowerInvariant() switch
        {
            ".cue" or ".bin" or ".iso" or ".chd" or ".mds" or ".mdf" or ".sms" or ".gg" or ".sg" or ".smd" or ".gen" or ".md" or ".32x" or ".pce" or ".nes" or ".sfc" or ".smc" or ".gba" or ".gb" or ".gbc" or ".zip" or ".7z" => true,
            _ => false,
        };
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{(long)value} {units[unit]}" : $"{value:0.0} {units[unit]}";
    }

    private static string Truncate(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return text;
        }

        return maxChars <= 3 ? text[..maxChars] : text[..(maxChars - 3)] + "...";
    }

    private readonly record struct DirectoryEntry(string Name, string Path);
    private readonly record struct FileEntry(string Name, string Path, string Extension, long Size, DateTime Modified);
    private enum FileSortMode
    {
        Name,
        Type,
        Size,
        Modified,
    }
}
