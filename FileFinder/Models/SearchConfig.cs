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
    }
}
