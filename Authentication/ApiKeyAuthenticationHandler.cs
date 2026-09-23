using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace INVERBANHN.Authentication;

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
        var apiKeySettings = _configuration.GetSection("ApiKeySettings");
        var headerName = apiKeySettings.GetValue<string>("HeaderName") ?? "X-Api-Key";
        var expectedApiKey = apiKeySettings.GetValue<string>("AppKey");

        // Si el header no viene, el manejador retorna NoResult para que otro
        // manejador (como el de JWT) intente autenticar.
        if (!Request.Headers.TryGetValue(headerName, out var extractedApiKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Si viene el header pero es incorrecto, retornamos falla.
        if (string.IsNullOrEmpty(expectedApiKey) || extractedApiKey.ToString() != expectedApiKey)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key provided."));
        }

        // Si la clave es válida, construimos la identidad con rol de SuperAdmin
        var claims = new[] { 
            new Claim(ClaimTypes.NameIdentifier, "SuperAdmin_ApiKey"),
            new Claim(ClaimTypes.Name, "Armando Banegas (API)"),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
