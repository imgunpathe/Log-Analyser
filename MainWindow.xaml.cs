using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LogAnalyser;

public partial class MainWindow : Window
{
    private readonly List<LogEntry> allEntries = new();
    private string currentFilterLevel = "ALL";
    private bool isDarkMode = false;

    // Eye-Comfort Dark Theme Brushes
    private static readonly Brush DarkErrBg = new SolidColorBrush(Color.FromRgb(45, 24, 26));
    private static readonly Brush DarkWarnBg = new SolidColorBrush(Color.FromRgb(42, 35, 18));
    private static readonly Brush DarkCrtBg = new SolidColorBrush(Color.FromRgb(53, 23, 59));

    private static readonly Brush DarkErrFg = new SolidColorBrush(Color.FromRgb(248, 113, 113));
    private static readonly Brush DarkWarnFg = new SolidColorBrush(Color.FromRgb(251, 191, 36));
    private static readonly Brush DarkCrtFg = new SolidColorBrush(Color.FromRgb(232, 121, 249));
    private static readonly Brush DarkInfFg = new SolidColorBrush(Color.FromRgb(74, 222, 128));
    private static readonly Brush DarkDbgFg = new SolidColorBrush(Color.FromRgb(56, 189, 248));
    private static readonly Brush DarkTrcFg = new SolidColorBrush(Color.FromRgb(148, 163, 184));
    private static readonly Brush DarkDefaultFg = new SolidColorBrush(Color.FromRgb(248, 250, 252));

    // Eye-Comfort Light Theme Brushes
    private static readonly Brush LightErrBg = new SolidColorBrush(Color.FromRgb(254, 242, 242));
    private static readonly Brush LightWarnBg = new SolidColorBrush(Color.FromRgb(254, 252, 232));
    private static readonly Brush LightCrtBg = new SolidColorBrush(Color.FromRgb(253, 244, 255));

    private static readonly Brush LightErrFg = new SolidColorBrush(Color.FromRgb(220, 38, 38));
    private static readonly Brush LightWarnFg = new SolidColorBrush(Color.FromRgb(217, 119, 6));
    private static readonly Brush LightCrtFg = new SolidColorBrush(Color.FromRgb(192, 38, 211));
    private static readonly Brush LightInfFg = new SolidColorBrush(Color.FromRgb(22, 163, 74));
    private static readonly Brush LightDbgFg = new SolidColorBrush(Color.FromRgb(2, 132, 199));
    private static readonly Brush LightTrcFg = new SolidColorBrush(Color.FromRgb(71, 85, 105));
    private static readonly Brush LightDefaultFg = new SolidColorBrush(Color.FromRgb(15, 23, 42));

    private System.Windows.Threading.DispatcherTimer? refreshTimer;
    private string? lastPath = null;

    public MainWindow()
    {
        InitializeComponent();
        LoadInfoText();
        UpdateFilterButtonsStyle();
        
        // Prevent memory leak by cleaning up timers on window close
        this.Closed += OnWindowClosed;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        StopRefreshTimer();
    }

    private void StopRefreshTimer()
    {
        if (refreshTimer != null)
        {
            refreshTimer.Stop();
            refreshTimer = null;
        }
    }

    private string? FindAboutPath()
    {
        var candidatePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "about.txt"),
            Path.Combine(Directory.GetCurrentDirectory(), "about.txt"),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "about.txt"))
        };

        foreach (var p in candidatePaths)
        {
            if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
        }

        return null;
    }

    private void LoadInfoText()
    {
        try
        {
            var aboutPath = FindAboutPath();
            if (aboutPath != null && File.Exists(aboutPath))
            {
                using var fs = new FileStream(aboutPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, Encoding.UTF8);
                var lines = new List<string>();
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line)) lines.Add(line.Trim());
                }

                foreach (var l in lines)
                {
                    var parts = l.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim().ToLower();
                        var val = parts[1].Trim();

                        if (key.Contains("version")) AboutVersionText.Text = $"v{val}";
                        else if (key.Contains("build")) AboutBuildText.Text = $"Build {val}";
                        else if (key.Contains("release")) AboutReleaseText.Text = val;
                    }
                }
            }
        }
        catch
        {
            // Graceful fallback if file is missing or unreadable
        }
    }

    private void OnSelectFolder(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Select a log file",
            Filter = "Log files (*.log;*.txt;*.csv)|*.log;*.txt;*.csv|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var path = dlg.FileName;
            LoadPath(path);
        }
    }

    private void OnReloadClicked(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(lastPath))
        {
            LoadPath(lastPath);
        }
    }

    private void OnScrollToTop(object sender, RoutedEventArgs e)
    {
        if (LogGrid.Items.Count > 0)
        {
            LogGrid.ScrollIntoView(LogGrid.Items[0]);
        }
    }

    private void OnScrollToBottom(object sender, RoutedEventArgs e)
    {
        if (LogGrid.Items.Count > 0)
        {
            LogGrid.ScrollIntoView(LogGrid.Items[LogGrid.Items.Count - 1]);
        }
    }

    private void OnExportClicked(object sender, RoutedEventArgs e)
    {
        if (LogGrid.ItemsSource is not List<LogEntry> currentItems || currentItems.Count == 0)
        {
            MessageBox.Show("No log entries to export.", "Export Logs", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        using var saveDlg = new System.Windows.Forms.SaveFileDialog
        {
            Title = "Export Filtered Logs",
            Filter = "Text file (*.txt)|*.txt|CSV file (*.csv)|*.csv|JSON file (*.json)|*.json",
            FileName = $"ExportedLogs_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (saveDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            try
            {
                var filePath = saveDlg.FileName;
                var ext = Path.GetExtension(filePath).ToLower();

                if (ext == ".json")
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("[");
                    for (int i = 0; i < currentItems.Count; i++)
                    {
                        var item = currentItems[i];
                        var jsonItem = $"  {{\"time\": \"{EscapeJson(item.Time)}\", \"level\": \"{item.Level}\", \"message\": \"{EscapeJson(item.Message)}\"}}";
                        sb.AppendLine(i < currentItems.Count - 1 ? jsonItem + "," : jsonItem);
                    }
                    sb.AppendLine("]");
                    File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                }
                else if (ext == ".csv")
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Timestamp,Level,Message");
                    foreach (var item in currentItems)
                    {
                        sb.AppendLine($"\"{EscapeCsv(item.Time)}\",\"{EscapeCsv(item.Level)}\",\"{EscapeCsv(item.Message)}\"");
                    }
                    File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                }
                else
                {
                    var lines = currentItems.Select(x => $"{x.Time} [{x.Level}] {x.Message}");
                    File.WriteAllLines(filePath, lines, Encoding.UTF8);
                }

                MessageBox.Show($"Successfully exported {currentItems.Count:N0} entries to:\n{filePath}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private static string EscapeJson(string str) => str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    private static string EscapeCsv(string str) => str.Replace("\"", "\"\"");

    private void OnClearClicked(object sender, RoutedEventArgs e)
    {
        allEntries.Clear();
        lastPath = null;
        FilePathText.Text = "No file or folder selected";
        DetailDrawer.Visibility = Visibility.Collapsed;
        UpdateCountsAndFilter();
    }

    private void LoadPath(string path)
    {
        LoadInfoText();
        lastPath = path;
        FilePathText.Text = path;
        allEntries.Clear();

        if (File.Exists(path))
        {
            ReadFile(path);
        }
        else if (Directory.Exists(path))
        {
            LoadFolder(path);
        }

        UpdateCountsAndFilter();

        if (AutoRefresh.IsChecked == true)
        {
            StartRefreshTimer();
        }
    }

    private void LoadFolder(string folder)
    {
        try
        {
            var files = Directory.EnumerateFiles(folder)
                .Where(f => f.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

            foreach (var file in files)
            {
                ReadFile(file);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error reading folder: {ex.Message}", "Folder Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ReadFile(string filePath)
    {
        try
        {
            // Use FileShare.ReadWrite to avoid locking issues when log files are being written to by another process
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs, Encoding.UTF8);

            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                var entry = ParseLine(line);
                if (entry != null)
                {
                    allEntries.Add(entry);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error reading {Path.GetFileName(filePath)}: {ex.Message}", "Read Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private LogEntry? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var bracketStart = line.IndexOf('[');
        var bracketEnd = line.IndexOf(']', Math.Max(0, bracketStart));

        if (bracketStart >= 0 && bracketEnd > bracketStart)
        {
            var tsStr = line.Substring(0, bracketStart).Trim();
            var rawLevel = line.Substring(bracketStart + 1, bracketEnd - bracketStart - 1).Trim().ToUpper();
            var msg = line.Substring(bracketEnd + 1).Trim();

            var level = rawLevel switch
            {
                "ERROR" or "ERR" => "ERR",
                "WARNING" or "WARN" or "WRN" => "WRN",
                "INFO" or "INFORMATION" or "INF" => "INF",
                "DEBUG" or "DBG" => "DBG",
                "TRACE" or "TRC" => "TRC",
                "CRITICAL" or "FATAL" or "CRT" => "CRT",
                _ => rawLevel.Length >= 3 ? rawLevel.Substring(0, 3) : rawLevel
            };

            return new LogEntry { Time = string.IsNullOrEmpty(tsStr) ? "-" : tsStr, Level = level, Message = msg };
        }

        var parts = line.Split(new[] { ' ' }, 2);
        return new LogEntry { Time = parts[0], Level = "INF", Message = parts.Length > 1 ? parts[1] : line };
    }

    private void LogGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.Item is not LogEntry entry) return;

        var lvl = entry.Level.ToUpper();

        if (isDarkMode)
        {
            e.Row.Background = lvl switch
            {
                "ERR" => DarkErrBg,
                "WRN" => DarkWarnBg,
                "CRT" => DarkCrtBg,
                _ => Brushes.Transparent
            };

            e.Row.Foreground = lvl switch
            {
                "ERR" => DarkErrFg,
                "WRN" => DarkWarnFg,
                "CRT" => DarkCrtFg,
                "INF" => DarkInfFg,
                "DBG" => DarkDbgFg,
                "TRC" => DarkTrcFg,
                _ => DarkDefaultFg
            };
        }
        else
        {
            e.Row.Background = lvl switch
            {
                "ERR" => LightErrBg,
                "WRN" => LightWarnBg,
                "CRT" => LightCrtBg,
                _ => Brushes.Transparent
            };

            e.Row.Foreground = lvl switch
            {
                "ERR" => LightErrFg,
                "WRN" => LightWarnFg,
                "CRT" => LightCrtFg,
                "INF" => LightInfFg,
                "DBG" => LightDbgFg,
                "TRC" => LightTrcFg,
                _ => LightDefaultFg
            };
        }
    }

    private void UpdateCountsAndFilter()
    {
        int total = allEntries.Count;
        int crt = 0, err = 0, wrn = 0, inf = 0, dbg = 0, trc = 0;

        foreach (var entry in allEntries)
        {
            switch (entry.Level)
            {
                case "CRT": crt++; break;
                case "ERR": err++; break;
                case "WRN": wrn++; break;
                case "INF": inf++; break;
                case "DBG": dbg++; break;
                case "TRC": trc++; break;
            }
        }

        BtnAll.Content = $"ALL ({total})";
        BtnCrt.Content = $"CRT ({crt})";
        BtnErr.Content = $"ERR ({err})";
        BtnWrn.Content = $"WRN ({wrn})";
        BtnInf.Content = $"INF ({inf})";
        BtnDbg.Content = $"DBG ({dbg})";
        BtnTrc.Content = $"TRC ({trc})";

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var searchText = FilterText.Text?.Trim() ?? "";
        bool matchCase = MatchCaseCheckBox.IsChecked == true;
        bool isRegex = RegexCheckBox.IsChecked == true;

        Regex? regex = null;
        if (isRegex && !string.IsNullOrEmpty(searchText))
        {
            try
            {
                var options = matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                regex = new Regex(searchText, options);
            }
            catch
            {
                // Invalid regex input; treat as invalid match
            }
        }

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        var filtered = allEntries.Where(x =>
        {
            var matchesLevel = currentFilterLevel switch
            {
                "ALL" => true,
                _ => x.Level.Equals(currentFilterLevel, StringComparison.OrdinalIgnoreCase)
            };

            if (!matchesLevel) return false;

            if (string.IsNullOrEmpty(searchText)) return true;

            if (isRegex)
            {
                if (regex == null) return false;
                return regex.IsMatch(x.Message) || regex.IsMatch(x.Time) || regex.IsMatch(x.Level);
            }

            return x.Message.Contains(searchText, comparison) ||
                   x.Time.Contains(searchText, comparison) ||
                   x.Level.Contains(searchText, comparison);
        }).ToList();

        LogGrid.ItemsSource = filtered;
        StatusMetricsText.Text = $"Showing {filtered.Count:N0} of {allEntries.Count:N0} log entries • Filter: {currentFilterLevel}";
    }

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
    {
        ClearSearchButton.Visibility = string.IsNullOrEmpty(FilterText.Text) ? Visibility.Collapsed : Visibility.Visible;
        ApplyFilter();
    }

    private void OnClearSearchClicked(object sender, RoutedEventArgs e)
    {
        FilterText.Text = "";
    }

    private void OnFilterOptionChanged(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void LogGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LogGrid.SelectedItem is LogEntry entry)
        {
            DetailDrawer.Visibility = Visibility.Visible;
            DetailTimeText.Text = $"Time: {entry.Time}";
            DetailLevelText.Text = $"Level: {entry.Level}";
            DetailMessageText.Text = entry.Message;
        }
    }

    private void OnCopySelectedLog(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(DetailMessageText.Text))
        {
            Clipboard.SetText(DetailMessageText.Text);
            MessageBox.Show("Log entry copied to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnCloseDetailDrawer(object sender, RoutedEventArgs e)
    {
        DetailDrawer.Visibility = Visibility.Collapsed;
        LogGrid.SelectedItem = null;
    }

    private void OnFilterLevel(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            currentFilterLevel = tag;
            UpdateFilterButtonsStyle();
            ApplyFilter();
        }
    }

    private void UpdateFilterButtonsStyle()
    {
        var buttons = new[] { BtnAll, BtnCrt, BtnErr, BtnWrn, BtnInf, BtnDbg, BtnTrc };
        var accentBrush = (Brush)Application.Current.Resources["AccentBrush"];
        var cardBrush = (Brush)Application.Current.Resources["CardBgBrush"];
        var textPrimaryBrush = (Brush)Application.Current.Resources["TextPrimaryBrush"];
        var borderBrush = (Brush)Application.Current.Resources["BorderBrush"];

        foreach (var b in buttons)
        {
            if (b == null) continue;
            bool isActive = (b.Tag?.ToString() == currentFilterLevel);

            if (isActive)
            {
                b.Background = accentBrush;
                b.Foreground = Brushes.White;
                b.BorderBrush = accentBrush;
            }
            else
            {
                b.Background = cardBrush;
                b.Foreground = textPrimaryBrush;
                b.BorderBrush = borderBrush;
            }
        }
    }

    private void OnAutoRefreshToggle(object sender, RoutedEventArgs e)
    {
        if (AutoRefresh.IsChecked == true)
        {
            StartRefreshTimer();
        }
        else
        {
            StopRefreshTimer();
        }
    }

    private void StartRefreshTimer()
    {
        StopRefreshTimer(); // Clean up existing timer to avoid leaks

        refreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };

        refreshTimer.Tick += (s, args) =>
        {
            if (!string.IsNullOrEmpty(lastPath))
            {
                allEntries.Clear();
                if (File.Exists(lastPath)) ReadFile(lastPath);
                else if (Directory.Exists(lastPath)) LoadFolder(lastPath);
                UpdateCountsAndFilter();

                // Live Tail: auto scroll to bottom if live tail is checked
                if (LiveTailCheckBox.IsChecked == true && LogGrid.Items.Count > 0)
                {
                    LogGrid.ScrollIntoView(LogGrid.Items[LogGrid.Items.Count - 1]);
                }
            }
        };

        refreshTimer.Start();
    }

    private void OnToggleTheme(object sender, RoutedEventArgs e)
    {
        isDarkMode = !isDarkMode;

        var themeUri = isDarkMode ? "Themes/Dark.xaml" : "Themes/Light.xaml";
        var dict = new ResourceDictionary { Source = new Uri(themeUri, UriKind.Relative) };

        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(dict);

        ThemeToggleButton.Content = isDarkMode ? "Dark Mode" : "Light Mode";
        UpdateFilterButtonsStyle();
        LogGrid.Items.Refresh();
    }
}

public class LogEntry
{
    public string Time { get; set; } = "";
    public string Level { get; set; } = "";
    public string Message { get; set; } = "";
}
