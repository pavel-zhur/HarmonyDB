using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using Syncfusion.HtmlConverter;
using Syncfusion.Licensing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;

namespace OneShelf.Pdfs.Api;

public class Convert
{
    public const string SyncFusionLicenseKey = nameof(SyncFusionLicenseKey);
    private const string BlinkBinariesPath = nameof(BlinkBinariesPath);

    private readonly IConfiguration _configuration;

    public Convert(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    [FunctionName("Convert")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ILogger log, ExecutionContext executionContext)
    {
        try
        {
            RegisterLicenseKey();

            string blinkBinariesPath;
            try
            {
                blinkBinariesPath = SetupBlinkBinaries(executionContext);
            }
            catch (Exception e)
            {
                throw new("BlinkBinaries initialization failed", e);
            }

            string requestBody;
            using (StreamReader streamReader = new StreamReader(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }

            //Initialize the HTML to PDF converter with the Blink rendering engine.
            HtmlToPdfConverter htmlConverter = new HtmlToPdfConverter(HtmlRenderingEngine.Blink);
            BlinkConverterSettings settings = new BlinkConverterSettings();

            //Set command line arguments to run without sandbox.
            settings.CommandLineArguments.Add("--no-sandbox");
            settings.CommandLineArguments.Add("--disable-setuid-sandbox");

            settings.BlinkPath = blinkBinariesPath;

            //disable JavaScript 
            settings.EnableJavaScript = false;
            settings.MediaType = MediaType.Print;

            settings.PdfFooter = CreateFooter();

            //Disable split images and text lines

            //Set blink view port size
            settings.ViewPortSize = new(512, 0);
            settings.PdfPageSize = PdfPageSize.A4;

            //Set PDF page margin 
            settings.Margin = new() { Top = 30, Left = 30, Right = 30, Bottom = 30 };

            //Assign WebKit settings to the HTML converter 
            htmlConverter.ConverterSettings = settings;

            //Convert URL to PDF
            using PdfDocument document = htmlConverter.Convert(requestBody, null);

            var pageCount = document.PageCount;

            MemoryStream ms = new MemoryStream();

            //Save and close the PDF document  
            document.Save(ms);
            document.Close();

            ms.Position = 0;

            return new FileStreamResult(ms, "application/pdf")
            {
                FileDownloadName = $"{pageCount}.pdf",
            };
        }
        catch (Exception e)
        {
            log.LogError(e, "Error converting the html.");
            return new ContentResult
            {
                StatusCode = 500,
                Content = e.ToString(),
                ContentType = "text/plain",
            };
        }
    }

    private void RegisterLicenseKey()
    {
        FusionLicenseProvider.IsBoldLicenseValidation = true;
        SyncfusionLicenseProvider.RegisterLicense(_configuration.GetValue<string>(SyncFusionLicenseKey));
    }

    private string SetupBlinkBinaries(ExecutionContext executionContext)
    {
        var path = _configuration.GetValue<string>(BlinkBinariesPath);
        if (path != null) return path;

        var blinkAppDir = Path.Combine(executionContext.FunctionAppDirectory, "bin/runtimes/linux/native");
        var tempBlinkDir = Path.GetTempPath();
        var chromePath = Path.Combine(tempBlinkDir, "chrome");

        if (!File.Exists(chromePath))
        {
            CopyFilesRecursively(blinkAppDir, tempBlinkDir);

            SetExecutablePermission(tempBlinkDir);
        }

        return tempBlinkDir;
    }

    private static void CopyFilesRecursively(string sourcePath, string targetPath)
    {
        //Create all the directories from the source to the destination path.
        foreach (var dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
        }

        //Copy all the files from the source path to the destination path.
        foreach (var newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
        {
            File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
        }
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "chmod")]
    internal static extern int Chmod(string path, FileAccessPermissions mode);

    private static void SetExecutablePermission(string tempBlinkDir)
    {
        FileAccessPermissions ExecutableFilePermissions = FileAccessPermissions.UserRead | FileAccessPermissions.UserWrite | FileAccessPermissions.UserExecute |
                                                          FileAccessPermissions.GroupRead | FileAccessPermissions.GroupExecute | FileAccessPermissions.OtherRead | FileAccessPermissions.OtherExecute;

        var executableFiles = new string[] { "chrome", "chrome_sandbox" };

        foreach (var executable in executableFiles)
        {
            var execPath = Path.Combine(tempBlinkDir, executable);

            if (File.Exists(execPath))
            {
                var code = Chmod(execPath, ExecutableFilePermissions);
                if (code != 0)
                {
                    throw new("Chmod operation failed");
                }
            }
        }
    }

    private static PdfPageTemplateElement CreateFooter()
    {
        RectangleF bounds = new RectangleF(0, 0, PdfPageSize.A4.Width, 30);

        //Create a new page template and assigning the bounds
        PdfPageTemplateElement footer = new PdfPageTemplateElement(bounds);

        PdfFont font = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
        PdfBrush brush = new PdfSolidBrush(Color.DarkGray);

        //Create page number field to show page numbering in footer, this field automatically get update for each page.
        PdfPageNumberField pageNumber = new PdfPageNumberField(font, brush);
        PdfPageCountField count = new PdfPageCountField(font, brush);
        PdfCompositeField pageNumberField = new PdfCompositeField(font, brush, "Page {0}/{1}", pageNumber, count);

        //Drawing page number field in footer
        pageNumberField.Draw(footer.Graphics, new((bounds.Width - 110), 20));

        return footer;
    }

    [Flags]
    internal enum FileAccessPermissions : uint
    {
        OtherExecute = 1,
        OtherWrite = 2,
        OtherRead = 4,

        GroupExecute = 8,
        GroupWrite = 16,
        GroupRead = 32,

        UserExecute = 64,
        UserWrite = 128,
        UserRead = 256
    }
}