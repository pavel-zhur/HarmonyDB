using System.Globalization;
using ExifLibrary;
using OneShelf.Common;

namespace OneShelf.Videos.BusinessLogic.Services;

public class ExifService
{
    public async Task<DateTime> SetExifTimestamp(string path, string tempFileName)
    {
        var image = await ImageFile.FromFileAsync(path);
        if (image.Errors.Any()) throw new($"Some image errors, {path}.");
        if (image.Properties.Contains(ExifTag.DateTime)) throw new($"The image contains a datetime, {path}.");

        var dateTime = DateTime.ParseExact(Path.GetFileNameWithoutExtension(path).SelectSingle(x => x.Substring(x.IndexOf('@') + 1)), "dd-MM-yyyy_HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

        image.Properties.Set(ExifTag.DateTime, dateTime);
        await image.SaveAsync(tempFileName);

        return dateTime;
    }
}