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
        private static readonly AuthenticationState _anonymousState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

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
            catch (InvalidOperationException)
            {
                return _anonymousState;
            }

            if (string.IsNullOrEmpty(token))
            {
                return _anonymousState;
            }
            
            var principal = new ClaimsPrincipal(new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt"));
            return new AuthenticationState(principal);
        }

        public void MarkUserAsAuthenticated(string token)
        {
            var authenticatedUser = new ClaimsPrincipal(new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt"));
            var authState = Task.FromResult(new AuthenticationState(authenticatedUser));
            NotifyAuthenticationStateChanged(authState);
        }

        public async Task MarkUserAsLoggedOut()
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
            var authState = Task.FromResult(_anonymousState);
            NotifyAuthenticationStateChanged(authState);
        }

        private IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var claims = new List<Claim>();
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

            if (keyValuePairs == null)
            {
                return claims;
            }
            
            // Buscar el ID del usuario (primero el 'sub' estándar, luego el de Microsoft)
            if (keyValuePairs.TryGetValue("sub", out var sub) && sub?.ToString() is string subValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, subValue));
            }
            else if (keyValuePairs.TryGetValue(ClaimTypes.NameIdentifier, out var nameId) && nameId?.ToString() is string nameIdValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, nameIdValue));
            }

            // Buscar el Nombre del usuario
            if (keyValuePairs.TryGetValue(ClaimTypes.Name, out var name) && name?.ToString() is string nameValue)
            {
                claims.Add(new Claim(ClaimTypes.Name, nameValue));
            }

            // Buscar el Email del usuario
            if (keyValuePairs.TryGetValue(ClaimTypes.Email, out var email) && email?.ToString() is string emailValue)
            {
                claims.Add(new Claim(ClaimTypes.Email, emailValue));
            }

            // Buscar Roles
            if (keyValuePairs.TryGetValue(ClaimTypes.Role, out var roles))
            {
                if (roles is JsonElement rolesElement && rolesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var role in rolesElement.EnumerateArray())
                    {
                        if(role.GetString() is string roleValue)
                        {
                            claims.Add(new Claim(ClaimTypes.Role, roleValue));
                        }
                    }
                }
                else if (roles?.ToString() is string singleRole)
                {
                    claims.Add(new Claim(ClaimTypes.Role, singleRole));
                }
            }

            return claims;
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