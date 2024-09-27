using ExifLibrary;

namespace OneShelf.Videos.BusinessLogic.Services;

public class ExifService
{
    public async Task SetExifTimestamp(string path, string tempFileName, DateTime timestamp)
    {
        var image = await ImageFile.FromFileAsync(path);
        if (image.Errors.Any()) throw new($"Some image errors, {path}.");
        if (image.Properties.Contains(ExifTag.DateTime)) throw new($"The image contains a datetime, {path}.");

        image.Properties.Set(ExifTag.DateTime, timestamp);
        await image.SaveAsync(tempFileName);
    }
}