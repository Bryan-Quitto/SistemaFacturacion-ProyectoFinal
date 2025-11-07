using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace FacturasSRI.Web.Services
{
    public class ApiAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<ApiAuthenticationStateProvider> _logger;

        public ApiAuthenticationStateProvider(IJSRuntime jsRuntime, ILogger<ApiAuthenticationStateProvider> logger)
        {
            _jsRuntime = jsRuntime;
            _logger = logger;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            string token;
            try
            {
                token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "authToken");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "IJSRuntime no disponible (probablemente pre-renderizado).");
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogInformation("No se encontró token en localStorage.");
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
            
            _logger.LogInformation("Token encontrado, parseando claims...");
            var identity = new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        public void MarkUserAsAuthenticated(string token)
{
    _logger.LogInformation($"MarkUserAsAuthenticated llamado. Notificando al sistema para que re-evalúe.");
    
    var authenticatedUser = new ClaimsPrincipal(new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt"));
    var authState = Task.FromResult(new AuthenticationState(authenticatedUser));
    
    NotifyAuthenticationStateChanged(authState);
}

public void MarkUserAsLoggedOut()
{
    _logger.LogInformation("MarkUserAsLoggedOut llamado. Notificando al sistema para que re-evalúe.");

    var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
    var authState = Task.FromResult(new AuthenticationState(anonymousUser));
    
    NotifyAuthenticationStateChanged(authState);
}

        private IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            _logger.LogInformation("--- Parseando JWT ---");
            var claims = new List<Claim>();
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

            if (keyValuePairs == null)
            {
                _logger.LogWarning("Payload del JWT es nulo o no se pudo deserializar.");
                return claims;
            }

            foreach (var kvp in keyValuePairs)
            {
                _logger.LogInformation($"Claim encontrado en Token -> Tipo: [{kvp.Key}], Valor: [{kvp.Value}]");
            }

            if (keyValuePairs.TryGetValue(ClaimTypes.Email, out var email) && email != null)
            {
                claims.Add(new Claim(ClaimTypes.Email, email.ToString()));
            }
            if (keyValuePairs.TryGetValue("sub", out var sub) && sub != null)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, sub.ToString()));
            }

            if (keyValuePairs.TryGetValue("role", out var roles) || 
                keyValuePairs.TryGetValue(ClaimTypes.Role, out roles))
            {
                _logger.LogInformation("Se encontró un claim de Rol.");
                if (roles is JsonElement rolesElement && rolesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var role in rolesElement.EnumerateArray())
                    {
                        if(role.GetString() != null)
                        {
                            var roleValue = role.GetString();
                            _logger.LogInformation($"Añadiendo Rol al Principal: {roleValue}");
                            claims.Add(new Claim(ClaimTypes.Role, roleValue));
                        }
                    }
                }
                else if (roles != null)
                {
                    var roleValue = roles.ToString();
                    _logger.LogInformation($"Añadiendo Rol (individual) al Principal: {roleValue}");
                    claims.Add(new Claim(ClaimTypes.Role, roleValue));
                }
            }
            else
            {
                _logger.LogWarning("No se encontró ningún claim 'role' o 'http://.../role' en el token.");
            }
            
            _logger.LogInformation("--- Parseo de JWT Terminado ---");
            return claims.AsEnumerable();
        }

        private byte[] ParseBase64WithoutPadding(string base64)
        {
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }
}