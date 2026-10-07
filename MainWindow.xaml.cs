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
            Filter = "Log & Text files (*.log*;*.txt*;*.csv*;TXT_*;LOG_*)|*.log*;*.txt*;*.csv*;TXT_*;LOG_*;*.log;*.txt;*.csv|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var path = dlg.FileName;
            LoadPath(path);
        }
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                LoadPath(files[0]);
            }
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
                .Where(f =>
                {
                    var name = Path.GetFileName(f);
                    return name.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
                           name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                           name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
                           name.Contains(".log", StringComparison.OrdinalIgnoreCase) ||
                           name.Contains(".txt", StringComparison.OrdinalIgnoreCase) ||
                           name.Contains(".csv", StringComparison.OrdinalIgnoreCase) ||
                           name.StartsWith("TXT_", StringComparison.OrdinalIgnoreCase) ||
                           name.StartsWith("LOG_", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(f => f);

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
            using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            string? line;
            LogEntry? lastEntry = null;

            while ((line = sr.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var entry = ParseLine(line);
                if (entry != null)
                {
                    allEntries.Add(entry);
                    lastEntry = entry;
                }
                else if (lastEntry != null)
                {
                    // Multiline continuation (e.g. stack traces, error details, multiline payloads)
                    lastEntry.Message += Environment.NewLine + line;
                }
                else
                {
                    // Standalone line without recognized timestamp
                    var fallback = new LogEntry { Time = "-", Level = "INF", Message = line.Trim(), RawLine = line };
                    allEntries.Add(fallback);
                    lastEntry = fallback;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error reading {Path.GetFileName(filePath)}: {ex.Message}", "Read Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // Regex patterns for comprehensive log parsing
    private static readonly Regex TimeStartPattern = new(
        @"^(?:\[|\()?(?<time>\d{4}[-/.]\d{2}[-/.]\d{2}[T ]\d{2}:\d{2}:\d{2}(?:[.,]\d+)?(?:Z|[+-]\d{2}:?\d{2})?|\d{2}[-/.]\d{2}[-/.]\d{4}[T ]\d{2}:\d{2}:\d{2}(?:[.,]\d+)?|(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s+\d{1,2}(?:\s+\d{4})?\s+\d{2}:\d{2}:\d{2}(?:[.,]\d+)?|\d{2}:\d{2}:\d{2}(?:[.,]\d+)?)(?:\]|\))?\s*(?:[-|:]\s*)?(?<rest>.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LevelStartPattern = new(
        @"^(?:\[(?<level>[A-Za-z]+)\]|(?<level>[A-Za-z]{3,12}))\s*(?:[-|:]\s*)?(?<time>\d{4}[-/.]\d{2}[-/.]\d{2}[T ]\d{2}:\d{2}:\d{2}(?:[.,]\d+)?|\d{2}[-/.]\d{2}[-/.]\d{4}[T ]\d{2}:\d{2}:\d{2}(?:[.,]\d+)?|\d{2}:\d{2}:\d{2}(?:[.,]\d+)?)\s*(?:[-|:]\s*)?(?<rest>.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CsvLogPattern = new(
        @"^[""']?(?<time>\d{4}[-/.]\d{2}[-/.]\d{2}[T ]\d{2}:\d{2}:\d{2}(?:[.,]\d+)?|\d{2}[-/.]\d{2}[-/.]\d{4}[T ]\d{2}:\d{2}:\d{2}(?:[.,]\d+)?)[""']?[\t,;]\s*[""']?(?<level>[A-Za-z]+)[""']?[\t,;]\s*[""']?(?<msg>.*?)[""']?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EmbeddedLevelPattern = new(
        @"(?:^|[\s\[\(\|,-])(?<level>CRITICAL|FATAL|EMERGENCY|ALERT|PANIC|CRT|CRIT|FTL|ERROR|SEVERE|EXCEPTION|ERRO|ERR|SEV|WARNING|WARN|WRN|INFORMATION|INFORMATIVE|NOTICE|INFO|INF|DEBUG|DEBG|FINEST|FINER|FINE|DBG|TRACE|VERBOSE|TRAC|VRB|TRC)(?:[\s\]\)\:,-]|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string NormalizeLevel(string rawLevel)
    {
        if (string.IsNullOrWhiteSpace(rawLevel)) return "INF";
        var upper = rawLevel.Trim().ToUpperInvariant();
        return upper switch
        {
            "CRITICAL" or "CRIT" or "CRT" or "FATAL" or "FTL" or "EMERGENCY" or "ALERT" or "PANIC" => "CRT",
            "ERROR" or "ERR" or "ERRO" or "SEVERE" or "EXCEPTION" or "SEV" => "ERR",
            "WARNING" or "WARN" or "WRN" => "WRN",
            "INFORMATION" or "INFO" or "INF" or "INFORMATIVE" or "NOTICE" => "INF",
            "DEBUG" or "DBG" or "DEBG" or "FINE" or "FINER" or "FINEST" => "DBG",
            "TRACE" or "TRC" or "TRAC" or "VERBOSE" or "VRB" => "TRC",
            _ => upper.Length >= 3 ? upper.Substring(0, 3) : upper
        };
    }

    private LogEntry? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        // 1. Check CSV / TSV format
        var csvMatch = CsvLogPattern.Match(line);
        if (csvMatch.Success)
        {
            var ts = csvMatch.Groups["time"].Value.Trim();
            var lvl = NormalizeLevel(csvMatch.Groups["level"].Value);
            var msg = csvMatch.Groups["msg"].Value.Trim();
            return new LogEntry { Time = ts, Level = lvl, Message = msg, RawLine = line };
        }

        // 2. Check Level at start format (e.g. [INFO] 2026-09-29 13:52:56,856 msg)
        var levelStartMatch = LevelStartPattern.Match(line);
        if (levelStartMatch.Success)
        {
            var lvl = NormalizeLevel(levelStartMatch.Groups["level"].Value);
            var ts = levelStartMatch.Groups["time"].Value.Trim();
            var msg = CleanMessage(levelStartMatch.Groups["rest"].Value);
            return new LogEntry { Time = ts, Level = lvl, Message = msg, RawLine = line };
        }

        // 3. Check Timestamp at start format (e.g. 2026-09-29 13:52:56,856 RZHPWIN001 INFO msg)
        var timeMatch = TimeStartPattern.Match(line);
        if (timeMatch.Success)
        {
            var ts = timeMatch.Groups["time"].Value.Trim();
            var rest = timeMatch.Groups["rest"].Value.Trim();

            // Locate level word anywhere in header portion of rest
            var lvlMatch = EmbeddedLevelPattern.Match(rest);
            if (lvlMatch.Success)
            {
                var lvl = NormalizeLevel(lvlMatch.Groups["level"].Value);
                var prefix = rest.Substring(0, lvlMatch.Index).Trim();
                if (prefix == "-" || prefix == "|" || prefix == ":") prefix = "";

                var suffix = rest.Substring(lvlMatch.Index + lvlMatch.Length).Trim();
                suffix = CleanMessage(suffix);

                string msg;
                if (!string.IsNullOrEmpty(prefix))
                {
                    msg = $"{prefix}  {suffix}";
                }
                else
                {
                    msg = suffix;
                }

                return new LogEntry { Time = ts, Level = lvl, Message = msg, RawLine = line };
            }

            // Timestamp matched, but no specific level keyword found -> default to INF
            return new LogEntry { Time = ts, Level = "INF", Message = CleanMessage(rest), RawLine = line };
        }

        // 4. Line does not have a timestamp at the start -> return null to allow multiline / continuation handling
        return null;
    }

    private static string CleanMessage(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return "";
        var trimmed = msg.Trim();
        // Strip common leading delimiters like "- ", ": ", "| "
        if (trimmed.StartsWith("- ") || trimmed.StartsWith(": ") || trimmed.StartsWith("| "))
        {
            trimmed = trimmed.Substring(2).TrimStart();
        }
        return trimmed;
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

    private static bool MatchesLevelSearch(string normalizedLevel, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return false;
        var q = query.Trim().ToUpperInvariant();
        return normalizedLevel switch
        {
            "INF" => q is "INF" or "INFO" or "INFORMATION" or "NOTICE",
            "ERR" => q is "ERR" or "ERROR" or "SEVERE" or "EXCEPTION" or "SEV",
            "WRN" => q is "WRN" or "WARN" or "WARNING",
            "CRT" => q is "CRT" or "CRIT" or "CRITICAL" or "FATAL" or "FTL",
            "DBG" => q is "DBG" or "DEBUG" or "FINE" or "DEBG",
            "TRC" => q is "TRC" or "TRACE" or "VERBOSE" or "VRB",
            _ => normalizedLevel.Equals(q, StringComparison.OrdinalIgnoreCase)
        };
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
                   x.Level.Contains(searchText, comparison) ||
                   MatchesLevelSearch(x.Level, searchText);
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
    public string RawLine { get; set; } = "";
}
