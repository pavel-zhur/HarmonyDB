using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;
using OneShelf.Videos.BusinessLogic.Services.GooglePhotosExtensions.NonInteractive;

namespace OneShelf.Videos.Telegram.Commands;

[AdminCommand("google_login", "Человеческий логин", "Залогиниться в гугл")]
public class GoogleLogin(Io io, ILogger<GoogleLogin> logger, GoogleNonInteractiveLogin googleNonInteractiveLogin) : Command(io)
{
    protected override async Task ExecuteQuickly()
    {
        try
        {
            var question = await googleNonInteractiveLogin.Login(null);
            if (question == null)
            {
                io.WriteLine("Логин успешный.");
                return;
            }

            Io.WriteLine(question);
            var response = io.FreeChoice("Response url:");
            await googleNonInteractiveLogin.Login(response);
        }
        catch (Exception e) when (e is not NeedDialogResponseException)
        {
            Io.WriteLine($"{e.GetType()} {e.Message}");
            logger.LogError(e);
        }
    }
}