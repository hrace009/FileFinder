using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FileFinder.Models;

namespace FileFinder.Services
{
    public record SearchProgress(
        int Current, int Total,
        string CurrentName, string CurrentPath,
        bool IsIndexing = false,
        long FilesScanned = 0);

    public class FileSearchService
    {
        /// <summary>
        /// Two-phase search:
        /// Phase 1 — Scan all paths ONCE to build a filename→paths index.
        /// Phase 2 — Look up each input name against the index (O(1) for Exact).
        /// This is ~50× faster than the naïve re-scan-per-name approach.
        /// </summary>
        public async IAsyncEnumerable<SearchResult> SearchAsync(
            IEnumerable<string> inputNames,
            SearchConfig config,
            IProgress<SearchProgress>? progress,
            [EnumeratorCancellation] CancellationToken ct)
        {
            var names = inputNames.ToList();
            int total = names.Count;

            var extFilter = config.ExtensionFilter
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant())
                .ToHashSet();

            var comparer = config.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            var comparison = config.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            // ── Phase 1: build file index from all paths ────────────────────
            progress?.Report(new SearchProgress(0, total, "Membangun indeks...", "", IsIndexing: true));

            var fileIndex = await BuildFileIndexAsync(
                config.SearchPaths, config.Recursive, extFilter, comparer,
                (scanned, path) => progress?.Report(
                    new SearchProgress(0, total, "Mengindeks...", path,
                        IsIndexing: true, FilesScanned: scanned)),
                ct);

            // ── Phase 2: match each name against the index ──────────────────
            for (int i = 0; i < names.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                string inputName = names[i].Trim();
                if (string.IsNullOrWhiteSpace(inputName)) continue;

                progress?.Report(new SearchProgress(i + 1, total, inputName, "", IsIndexing: false));

                var result = new SearchResult
                {
                    RowNumber = i + 1,
                    InputName = inputName,
                    Status = SearchStatus.Pending
                };

                var matches = FindMatches(inputName, config.SearchType, fileIndex, comparison);

                if (matches.Count == 0)
                {
                    result.Status = SearchStatus.NotFound;
                }
                else if (matches.Count == 1)
                {
                    var m = matches[0];
                    result.Status = SearchStatus.Found;
                    result.FoundFileName = m.FileName;
                    result.FullPath = m.FullPath;
                    result.FileSize = GetFileSize(m.FullPath);
                    result.ModifiedDate = GetModifiedDate(m.FullPath);
                    result.AllMatches.Add(m.FullPath);
                }
                else
                {
                    var first = matches[0];
                    result.Status = SearchStatus.MultipleFound;
                    result.FoundFileName = first.FileName;
                    result.FullPath = first.FullPath;
                    result.FileSize = GetFileSize(first.FullPath);
                    result.ModifiedDate = GetModifiedDate(first.FullPath);
                    result.AllMatches.AddRange(matches.Select(m => m.FullPath));
                }

                yield return result;
                await Task.Yield();
            }
        }

        // ── Index builder ──────────────────────────────────────────────────

        private static async Task<Dictionary<string, List<(string FileName, string FullPath)>>>
            BuildFileIndexAsync(
                List<string> searchPaths,
                bool recursive,
                HashSet<string> extFilter,
                StringComparer comparer,
                Action<long, string> onProgress,
                CancellationToken ct)
        {
            var bag = new ConcurrentDictionary<string, ConcurrentBag<(string, string)>>(comparer);
            long totalScanned = 0;

            var enumOptions = new EnumerationOptions
            {
                RecurseSubdirectories = recursive,
                IgnoreInaccessible = true,          // silently skip access-denied dirs (no try/catch per file)
                AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint,
            };

            var validPaths = searchPaths.Where(Directory.Exists).ToList();

            await Task.Run(() =>
            {
                var parallelOptions = new ParallelOptions
                {
                    // Parallelize across separate paths (helps for multiple network shares)
                    MaxDegreeOfParallelism = Math.Max(1, Math.Min(validPaths.Count, 4)),
                    CancellationToken = ct
                };

                Parallel.ForEach(validPaths, parallelOptions, rootPath =>
                {
                    long localCount = 0;

                    foreach (string filePath in Directory.EnumerateFiles(rootPath, "*", enumOptions))
                    {
                        if (ct.IsCancellationRequested) return;

                        if (extFilter.Count > 0)
                        {
                            string ext = Path.GetExtension(filePath).ToLowerInvariant();
                            if (!extFilter.Contains(ext)) continue;
                        }

                        string fileName = Path.GetFileName(filePath);
                        bag.GetOrAdd(fileName, _ => new ConcurrentBag<(string, string)>())
                           .Add((fileName, filePath));

                        localCount++;
                        if (localCount % 2000 == 0)
                        {
                            long running = Interlocked.Add(ref totalScanned, localCount);
                            localCount = 0;
                            onProgress(running, rootPath);
                        }
                    }

                    Interlocked.Add(ref totalScanned, localCount);
                    onProgress(Interlocked.Read(ref totalScanned), rootPath);
                });
            }, ct);

            return bag.ToDictionary(kv => kv.Key, kv => kv.Value.ToList(), comparer);
        }

        // ── Name matcher ───────────────────────────────────────────────────

        private static List<(string FileName, string FullPath)> FindMatches(
            string inputName,
            SearchType searchType,
            Dictionary<string, List<(string FileName, string FullPath)>> index,
            StringComparison comparison)
        {
            if (searchType == SearchType.Exact)
            {
                return index.TryGetValue(inputName, out var list) ? list : new();
            }

            var result = new List<(string, string)>();
            foreach (var kv in index)
            {
                bool match = searchType == SearchType.Contains
                    ? kv.Key.Contains(inputName, comparison)
                    : IsWildcardMatch(kv.Key, inputName, comparison);
                if (match) result.AddRange(kv.Value);
            }
            return result;
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static long GetFileSize(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        private static DateTime GetModifiedDate(string path)
        {
            try { return new FileInfo(path).LastWriteTime; }
            catch { return DateTime.MinValue; }
        }

        private static bool IsWildcardMatch(string fileName, string pattern, StringComparison comparison)
        {
            try
            {
                var options = comparison == StringComparison.OrdinalIgnoreCase
                    ? RegexOptions.IgnoreCase : RegexOptions.None;

                string regexPattern = "^" +
                    Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";

                return Regex.IsMatch(fileName, regexPattern, options);
            }
            catch { return false; }
        }
    }
}

