using System.Collections.Generic;

namespace FileFinder.Models
{
    public enum SearchType
    {
        Exact,
        Contains,
        Wildcard
    }

    public class SearchConfig
    {
        public List<string> SearchPaths { get; set; } = new();
        public SearchType SearchType { get; set; } = SearchType.Contains;
        public bool Recursive { get; set; } = true;
        public bool CaseSensitive { get; set; } = false;

        /// <summary>
        /// Optional extension filter, e.g. [".pdf", ".docx"]. Empty = all types.
        /// </summary>
        public List<string> ExtensionFilter { get; set; } = new();

        /// <summary>
        /// Optional folder name filter. When non-empty, only files inside a directory
        /// whose name matches one of these values will be indexed. Empty = all folders.
        /// </summary>
        public List<string> FolderFilter { get; set; } = new();
    }
}
