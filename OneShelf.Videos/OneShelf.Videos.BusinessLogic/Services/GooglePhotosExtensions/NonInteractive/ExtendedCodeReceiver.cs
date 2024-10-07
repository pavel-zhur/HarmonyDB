using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using System.Collections.Specialized;
using System.Web;

namespace OneShelf.Videos.BusinessLogic.Services.GooglePhotosExtensions.NonInteractive;

public class ExtendedCodeReceiver : ICodeReceiver
{
    private readonly Func<Task> _responseGetter;
    
    private string? _response;

    public ExtendedCodeReceiver(Func<Task> responseGetter)
    {
        _responseGetter = responseGetter;
    }

    public async Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(AuthorizationCodeRequestUrl url, CancellationToken taskCancellationToken)
    {
        Question = url.Build().AbsoluteUri;
        await _responseGetter();
        NameValueCollection coll = HttpUtility.ParseQueryString(new Uri(_response ?? throw new("Could not have happened. The response getter is finished but the response is not initialized.")).Query);
        return new(coll.AllKeys.ToDictionary(k => k, k => coll[k]));
    }

    public string? Question { get; set; }

    public string RedirectUri => "https://127.0.0.1:42218/authorize/";

    public void GotResponse(string response) => _response = response;
}