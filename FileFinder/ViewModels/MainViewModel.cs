using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FileFinder.Models;
using FileFinder.Services;
using Microsoft.Win32;

namespace FileFinder.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly FileSearchService _searchService = new();
        private CancellationTokenSource? _cts;

        // ── Input ─────────────────────────────────────────────────────────────
        private string _inputText = string.Empty;
        public string InputText
        {
            get => _inputText;
            set
            {
                _inputText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(InputLineCount));
            }
        }

        public int InputLineCount =>
            string.IsNullOrWhiteSpace(_inputText)
                ? 0
                : _inputText.Split('\n').Count(l => l.Trim().Length > 0);

        // ── Search Paths ──────────────────────────────────────────────────────
        public ObservableCollection<string> SearchPaths { get; } = new();

        private string _newPathText = string.Empty;
        public string NewPathText
        {
            get => _newPathText;
            set { _newPathText = value; OnPropertyChanged(); }
        }

        private string? _selectedPath;
        public string? SelectedPath
        {
            get => _selectedPath;
            set { _selectedPath = value; OnPropertyChanged(); }
        }

        // ── Search Options ────────────────────────────────────────────────────
        private SearchType _selectedSearchType = SearchType.Contains;
        public SearchType SelectedSearchType
        {
            get => _selectedSearchType;
            set { _selectedSearchType = value; OnPropertyChanged(); }
        }

        private bool _isRecursive = true;
        public bool IsRecursive
        {
            get => _isRecursive;
            set { _isRecursive = value; OnPropertyChanged(); }
        }

        private bool _isCaseSensitive = false;
        public bool IsCaseSensitive
        {
            get => _isCaseSensitive;
            set { _isCaseSensitive = value; OnPropertyChanged(); }
        }

        private string _extensionFilter = string.Empty;
        public string ExtensionFilter
        {
            get => _extensionFilter;
            set { _extensionFilter = value; OnPropertyChanged(); }
        }

        // ── State ─────────────────────────────────────────────────────────────
        private bool _isSearching = false;
        public bool IsSearching
        {
            get => _isSearching;
            set { _isSearching = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotSearching)); }
        }
        public bool IsNotSearching => !_isSearching;

        private bool _isIndeterminate = false;
        /// <summary>True while preparing (no results yet) → shows scanning animation. False once items start arriving.</summary>
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set { _isIndeterminate = value; OnPropertyChanged(); }
        }

        private int _progressValue = 0;
        public int ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(); }
        }

        private int _progressMax = 100;
        public int ProgressMax
        {
            get => _progressMax;
            set { _progressMax = value; OnPropertyChanged(); }
        }

        private string _statusText = "Siap";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private string _summaryText = string.Empty;
        public string SummaryText
        {
            get => _summaryText;
            set { _summaryText = value; OnPropertyChanged(); }
        }

        // ── Results ───────────────────────────────────────────────────────────
        public ObservableCollection<SearchResult> Results { get; } = new();

        private SearchResult? _selectedResult;
        public SearchResult? SelectedResult
        {
            get => _selectedResult;
            set { _selectedResult = value; OnPropertyChanged(); }
        }

        // ── Commands ──────────────────────────────────────────────────────────
        public ICommand SearchCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ClearInputCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        public ICommand AddPathCommand { get; }
        public ICommand RemovePathCommand { get; }
        public ICommand ClearResultsCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand OpenInExplorerCommand { get; }
        public ICommand CopyFullPathCommand { get; }
        public ICommand CopyFileNameCommand { get; }
        public ICommand ShowAllMatchesCommand { get; }

        public MainViewModel()
        {
            SearchCommand = new RelayCommand(ExecuteSearch, () => IsNotSearching && SearchPaths.Count > 0 && !string.IsNullOrWhiteSpace(InputText));
            CancelCommand = new RelayCommand(ExecuteCancel, () => IsSearching);
            ClearInputCommand = new RelayCommand(() => InputText = string.Empty, () => IsNotSearching);
            ImportCommand = new RelayCommand(ExecuteImport, () => IsNotSearching);
            BrowseFolderCommand = new RelayCommand(ExecuteBrowseFolder);
            AddPathCommand = new RelayCommand(ExecuteAddPath, () => !string.IsNullOrWhiteSpace(NewPathText));
            RemovePathCommand = new RelayCommand(ExecuteRemovePath, () => SelectedPath != null);
            ClearResultsCommand = new RelayCommand(() => { Results.Clear(); SummaryText = string.Empty; }, () => Results.Count > 0);
            ExportCsvCommand = new RelayCommand(ExecuteExportCsv, () => Results.Count > 0);
            OpenInExplorerCommand = new RelayCommand(ExecuteOpenInExplorer, () => SelectedResult?.FullPath != null);
            CopyFullPathCommand = new RelayCommand(ExecuteCopyFullPath, () => SelectedResult?.FullPath != null);
            CopyFileNameCommand = new RelayCommand(ExecuteCopyFileName, () => SelectedResult?.FoundFileName != null);
            ShowAllMatchesCommand = new RelayCommand(ExecuteShowAllMatches, () => SelectedResult?.Status == SearchStatus.MultipleFound);
        }

        // ── Command Implementations ───────────────────────────────────────────
        private async void ExecuteSearch()
        {
            var names = InputText
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count == 0)
            {
                MessageBox.Show("Tidak ada nama file yang diinput.", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Results.Clear();
            SummaryText = string.Empty;
            IsSearching = true;
            IsIndeterminate = true;   // scanning animation until first result
            ProgressMax = names.Count;
            ProgressValue = 0;
            StatusText = "Mempersiapkan pencarian...";

            _cts = new CancellationTokenSource();

            var config = new SearchConfig
            {
                SearchPaths = SearchPaths.ToList(),
                SearchType = SelectedSearchType,
                Recursive = IsRecursive,
                CaseSensitive = IsCaseSensitive,
                ExtensionFilter = ParseExtensions(ExtensionFilter)
            };

            var progress = new Progress<SearchProgress>(p =>
            {
                if (p.IsIndexing)
                {
                    // Phase 1: indexing — keep indeterminate spinning
                    StatusText = p.FilesScanned > 0
                        ? $"Mengindeks file... {p.FilesScanned:N0} file ditemukan"
                        : "Mempersiapkan indeks...";
                }
                else
                {
                    // Phase 2: matching — switch to determinate progress bar
                    if (IsIndeterminate) IsIndeterminate = false;
                    ProgressValue = p.Current;
                    StatusText = $"Mencari '{p.CurrentName}' ... ({p.Current}/{p.Total})";
                }
            });

            try
            {
                await foreach (var result in _searchService.SearchAsync(names, config, progress, _cts.Token))
                {
                    Results.Add(result);
                }

                int found = Results.Count(r => r.Status == SearchStatus.Found);
                int notFound = Results.Count(r => r.Status == SearchStatus.NotFound);
                SummaryText = $"Selesai — Ditemukan: {found}  |  Tidak Ditemukan: {notFound}  |  Total: {Results.Count}";
                StatusText = "Pencarian selesai.";
            }
            catch (OperationCanceledException)
            {
                StatusText = "Pencarian dibatalkan.";
                SummaryText = $"Dibatalkan — Ditemukan: {Results.Count(r => r.Status == SearchStatus.Found)} dari {Results.Count} yang sudah diproses.";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsSearching = false;
                IsIndeterminate = false;
                ProgressValue = ProgressMax;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void ExecuteCancel()
        {
            _cts?.Cancel();
            StatusText = "Membatalkan...";
        }

        private void ExecuteImport()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import Daftar Nama File",
                Filter = "Text files (*.txt)|*.txt|CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var lines = File.ReadAllLines(dlg.FileName)
                    .Select(l =>
                    {
                        // Handle CSV: take only first column if comma-delimited
                        int comma = l.IndexOf(',');
                        return (comma >= 0 ? l[..comma] : l).Trim().Trim('"');
                    })
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                InputText = string.Join(Environment.NewLine, lines);
                StatusText = $"Imported {lines.Count} nama dari {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal membuka file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteBrowseFolder()
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Pilih Folder Pencarian",
                Multiselect = true
            };

            if (dlg.ShowDialog() != true) return;

            foreach (string path in dlg.FolderNames)
            {
                if (!SearchPaths.Contains(path))
                    SearchPaths.Add(path);
            }

            if (dlg.FolderNames.Length > 0)
                NewPathText = dlg.FolderNames[^1];
        }

        private void ExecuteAddPath()
        {
            string path = NewPathText.Trim();
            if (string.IsNullOrWhiteSpace(path)) return;

            if (!SearchPaths.Contains(path))
            {
                SearchPaths.Add(path);
            }
            NewPathText = string.Empty;
        }

        private void ExecuteRemovePath()
        {
            if (SelectedPath != null)
                SearchPaths.Remove(SelectedPath);
        }

        private void ExecuteExportCsv()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Export Hasil ke CSV",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"FileFinder_Hasil_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                DefaultExt = ".csv"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("No,Nama Input,Nama File Ditemukan,Full Path,Ukuran,Tanggal Modifikasi,Status,Semua Path");

                foreach (var r in Results)
                {
                    string allPaths = r.AllMatches.Count > 1
                        ? string.Join(" | ", r.AllMatches)
                        : r.FullPath ?? "";

                    sb.AppendLine(string.Join(",",
                        r.RowNumber,
                        CsvEscape(r.InputName),
                        CsvEscape(r.FoundFileName ?? ""),
                        CsvEscape(r.FullPath ?? ""),
                        CsvEscape(r.FileSizeDisplay),
                        CsvEscape(r.ModifiedDateDisplay),
                        CsvEscape(r.StatusDisplay),
                        CsvEscape(allPaths)
                    ));
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                StatusText = $"Exported {Results.Count} baris ke {Path.GetFileName(dlg.FileName)}";

                // Open the file in default application
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dlg.FileName,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal export: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteOpenInExplorer()
        {
            string? path = SelectedResult?.FullPath;
            if (path == null) return;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal membuka explorer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteCopyFullPath()
        {
            string? path = SelectedResult?.FullPath;
            if (path != null)
            {
                Clipboard.SetText(path);
                StatusText = $"Disalin: {path}";
            }
        }

        private void ExecuteCopyFileName()
        {
            string? name = SelectedResult?.FoundFileName;
            if (name != null)
            {
                Clipboard.SetText(name);
                StatusText = $"Disalin: {name}";
            }
        }

        private void ExecuteShowAllMatches()
        {
            var result = SelectedResult;
            if (result == null || result.AllMatches.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine($"Semua lokasi untuk '{result.InputName}':");
            sb.AppendLine();
            for (int i = 0; i < result.AllMatches.Count; i++)
                sb.AppendLine($"{i + 1}. {result.AllMatches[i]}");

            MessageBox.Show(sb.ToString(), $"Multiple Matches — {result.AllMatches.Count} file ditemukan",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static List<string> ParseExtensions(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return new();

            return input
                .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.StartsWith('.') ? e : "." + e)
                .ToList();
        }

        private static string CsvEscape(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
