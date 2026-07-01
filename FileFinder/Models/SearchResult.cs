using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileFinder.Models
{
    public enum SearchStatus
    {
        Pending,
        Found,
        NotFound,
        MultipleFound
    }

    public class SearchResult : INotifyPropertyChanged
    {
        private SearchStatus _status = SearchStatus.Pending;
        private string? _foundFileName;
        private string? _fullPath;
        private long? _fileSize;
        private DateTime? _modifiedDate;

        public int RowNumber { get; set; }
        public string InputName { get; set; } = string.Empty;

        public string? FoundFileName
        {
            get => _foundFileName;
            set { _foundFileName = value; OnPropertyChanged(); }
        }

        public string? FullPath
        {
            get => _fullPath;
            set { _fullPath = value; OnPropertyChanged(); }
        }

        public long? FileSize
        {
            get => _fileSize;
            set { _fileSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileSizeDisplay)); }
        }

        public DateTime? ModifiedDate
        {
            get => _modifiedDate;
            set { _modifiedDate = value; OnPropertyChanged(); OnPropertyChanged(nameof(ModifiedDateDisplay)); }
        }

        public SearchStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplay)); }
        }

        /// <summary>All matching paths when multiple files with the same name are found.</summary>
        public List<string> AllMatches { get; set; } = new();

        // Computed display properties
        public string FileSizeDisplay => FileSize.HasValue ? FormatBytes(FileSize.Value) : "-";
        public string ModifiedDateDisplay => ModifiedDate.HasValue ? ModifiedDate.Value.ToString("yyyy-MM-dd HH:mm") : "-";
        public string StatusDisplay => Status switch
        {
            SearchStatus.Found => "Ditemukan",
            SearchStatus.NotFound => "Tidak Ditemukan",
            SearchStatus.MultipleFound => $"Ditemukan ({AllMatches.Count}x)",
            _ => "Menunggu"
        };

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
