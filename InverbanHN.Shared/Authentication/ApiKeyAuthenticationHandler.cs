using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InverbanHN.Shared.Authentication;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headerName = _configuration["ApiKeySettings:HeaderName"] ?? "X-Api-Key";
        var expectedKey = _configuration["ApiKeySettings:AppKey"] ?? "IVB-ADMIN-ARMANDO-7722-BANEGAS-9911";

        if (!Request.Headers.TryGetValue(headerName, out var extractedApiKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!string.Equals(extractedApiKey, expectedKey, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("API Key no válida."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "SuperAdmin_ApiKey"),
            new Claim(ClaimTypes.Role, "SuperAdmin"),
            new Claim("Store_ID", "1")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
