using System.Net.Http.Headers;

namespace MyShowtime.Client.Services;

/// <summary>
/// HTTP message handler that adds user authentication headers to outgoing requests
/// </summary>
public class AuthenticatedHttpMessageHandler : DelegatingHandler
{
    private readonly UserStateService _userStateService;

    public AuthenticatedHttpMessageHandler(UserStateService userStateService)
    {
        _userStateService = userStateService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Add user ID header if user is logged in
        if (_userStateService.CurrentUser != null)
        {
            request.Headers.Add("X-User-Id", _userStateService.CurrentUser.Id.ToString());
        }

        return await base.SendAsync(request, cancellationToken);
    }
}