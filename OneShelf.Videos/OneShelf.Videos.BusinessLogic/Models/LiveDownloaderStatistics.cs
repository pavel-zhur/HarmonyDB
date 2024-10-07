namespace OneShelf.Videos.BusinessLogic.Models;

public class LiveDownloaderStatistics
{
    private int _downloaded;

    public int Chats { get; set; }
    public int Topics { get; set; }
    public int Mediae { get; set; }
    public int? ToDownload { get; set; }
    public int Downloaded => _downloaded;

    public void IncDownloaded() => Interlocked.Increment(ref _downloaded);
}