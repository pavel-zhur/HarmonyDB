using ExifLibrary;

namespace OneShelf.Videos.BusinessLogic.Services;

public class ExifService
{
    public async Task<bool> SetExifTimestamp(string path, string tempFileName, DateTime timestamp)
    {
        var image = await ImageFile.FromFileAsync(path);
        if (image.Errors.Any()) throw new($"Some image errors, {path}.");
        if (image.Properties.Contains(ExifTag.DateTime))
        {
            File.Copy(path, tempFileName);
            return false;
        }

        image.Properties.Set(ExifTag.DateTime, timestamp);
        await image.SaveAsync(tempFileName);

        return true;
    }
}