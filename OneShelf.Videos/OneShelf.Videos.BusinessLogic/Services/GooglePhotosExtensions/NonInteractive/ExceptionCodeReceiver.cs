using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;

namespace OneShelf.Videos.BusinessLogic.Services.GooglePhotosExtensions.NonInteractive;

public class ExceptionCodeReceiver : ICodeReceiver
{
    public async Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(AuthorizationCodeRequestUrl url, CancellationToken taskCancellationToken)
    {
        throw new NonInteractiveAuthenticationNeededException();
    }

    public string RedirectUri => throw new NonInteractiveAuthenticationNeededException();
}