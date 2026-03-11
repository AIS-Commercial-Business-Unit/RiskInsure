public class HttpsDirectoryListingParser : IFileListParser
/// <summary>
/// Parses and retrieves files from an NGINX autoindex directory listing served over HTTPS.
/// </summary>
/// <remarks>
/// This class handles HTTP requests to NGINX directory listings and extracts file information
/// using regex pattern matching. It supports parsing file names, URLs, dates, and sizes from
/// the HTML autoindex response.
/// </remarks>
public class NginxDirectoryListingParser
{
    /// <summary>
    /// Asynchronously retrieves and parses files from an NGINX autoindex directory listing.
    /// </summary>
    /// <param name="directoryUrl">The base URL of the NGINX directory listing.</param>
    /// <returns>A task that returns a list of NginxFileEntry objects with file metadata.</returns>
    /// <remarks>
    /// Extracts file information (name, URL, date, size) from NGINX autoindex HTML using regex.
    /// The parent directory link ("../") is automatically excluded from results.
    /// Date and size values are optional and will be null if not present in the HTML.
    /// </remarks>
    public static List<NginxFileEntry> ParseNginxFileList(string responseContent)
    {
        // Regex pattern for NGINX autoindex rows
        var pattern = new Regex(
            @"<a href=""(?<url>[^""]+)"">(?<name>[^<]+)</a>\s*(?<date>\d{2}-\w{3}-\d{4} \d{2}:\d{2})?\s*(?<size>\d+)?",
            RegexOptions.Compiled);

        var results = new List<NginxFileEntry>();

        foreach (Match match in pattern.Matches(responseContent))
        {
            var name = match.Groups["name"].Value;
            var url = match.Groups["url"].Value;

            // Skip parent directory link
            if (name == "../")
                continue;

            DateTime? date = null;
            if (DateTime.TryParse(match.Groups["date"].Value, out var parsedDate))
                date = parsedDate;

            long? size = null;
            if (long.TryParse(match.Groups["size"].Value, out var parsedSize))
                size = parsedSize;

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