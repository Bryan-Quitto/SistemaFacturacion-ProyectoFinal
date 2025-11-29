using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace FacturasSRI.Web.Services
{
    public class CookieAuthenticationStateProvider : ServerAuthenticationStateProvider, IDisposable
    {
        private readonly IServiceScope _scope;
        private readonly ILogger<CookieAuthenticationStateProvider> _logger;
        private readonly HttpContext? _httpContext;

        public CookieAuthenticationStateProvider(
            IServiceProvider serviceProvider,
            ILogger<CookieAuthenticationStateProvider> logger)
        {
            _scope = serviceProvider.CreateScope();
            _logger = logger;
            _httpContext = _scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext;
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            if (_httpContext?.User?.Identities.Any(i => i.IsAuthenticated) ?? false)
            {
                var user = _httpContext.User;
                var primaryIdentity = user.Identities.FirstOrDefault(i => i.IsAuthenticated);
                _logger.LogInformation("CookieAuthenticationStateProvider: Usuario AUTENTICADO encontrado. Esquema: {AuthenticationType}, Usuario: {UserName}", 
                    primaryIdentity?.AuthenticationType, primaryIdentity?.Name);
                return Task.FromResult(new AuthenticationState(user));
            }
            
            _logger.LogInformation("CookieAuthenticationStateProvider: No se encontró usuario autenticado. Devolviendo estado anónimo.");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}