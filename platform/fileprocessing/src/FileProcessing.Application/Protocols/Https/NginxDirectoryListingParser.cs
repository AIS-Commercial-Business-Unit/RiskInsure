using System.Text.RegularExpressions;

namespace FileProcessing.Application.Protocols;

/// <summary>
/// Parses files from an NGINX autoindex directory listing served over HTTPS.
/// </summary>
public static class NginxDirectoryListingParser
{
    /// <summary>
    /// Parses an NGINX autoindex HTML response into a collection of file entries.
    /// </summary>
    public static List<NginxFileEntry> ParseNginxFileList(string responseContent, string directoryUrl)
    {
        var pattern = new Regex(
            @"<a href=""(?<url>[^""]+)"">(?<name>[^<]+)</a>\s*(?<date>\d{2}-\w{3}-\d{4} \d{2}:\d{2})?\s*(?<size>\d+)?",
            RegexOptions.Compiled);

        var results = new List<NginxFileEntry>();

        foreach (Match match in pattern.Matches(responseContent))
        {
            var name = match.Groups["name"].Value;
            var url = match.Groups["url"].Value;

            if (name == "../")
            {
                continue;
            }

            DateTime? date = null;
            if (DateTime.TryParse(match.Groups["date"].Value, out var parsedDate))
            {
                date = parsedDate;
            }

            long? size = null;
            if (long.TryParse(match.Groups["size"].Value, out var parsedSize))
            {
                size = parsedSize;
            }

            results.Add(new NginxFileEntry
            {
                Name = name,
                Url = directoryUrl.TrimEnd('/') + "/" + url,
                Date = date,
                Size = size
            });
        }

        return results;
    }
}