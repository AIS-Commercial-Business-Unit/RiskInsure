namespace FileRetrieval.Application.Protocols;

public class NginxFileEntry
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long? Size { get; set; }
    public DateTime? Date { get; set; }
}