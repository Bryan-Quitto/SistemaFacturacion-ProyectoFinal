using System.Security.Claims;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FacturasSRI.Web.States
{
    public class CurrentUserState
    {
        public string? Token { get; private set; }
        public bool IsLoggedIn => !string.IsNullOrEmpty(Token);
        public string Role { get; private set; } = string.Empty;
        public string Email { get; private set; } = string.Empty;
        public string FullName { get; private set; } = string.Empty;
        public Guid UserId { get; private set; }
        
        public bool IsLoading { get; private set; } = true;

        public event Action? OnChange;

        public void SetUser(string token)
        {
            Console.WriteLine("--- CurrentUserState: SetUser LLAMADO ---");
            Token = token;
            if (!string.IsNullOrEmpty(Token))
            {
                var claims = ParseClaimsFromJwt(Token);
                UserId = Guid.Parse(claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? Guid.Empty.ToString());
                Email = claims.FirstOrDefault(c => c.Type == "email")?.Value ?? string.Empty;
                Role = claims.FirstOrDefault(c => c.Type == "role")?.Value ?? string.Empty;
                FullName = claims.FirstOrDefault(c => c.Type == "name")?.Value ?? string.Empty;
            }
            IsLoading = false;
            Console.WriteLine("--- CurrentUserState: SetUser finalizado. Disparando OnChange. ---");
            NotifyStateChanged();
        }

        public void Logout()
        {
            Console.WriteLine("--- CurrentUserState: Logout LLAMADO ---");
            Token = null;
            UserId = Guid.Empty;
            Email = string.Empty;
            Role = string.Empty;
            FullName = string.Empty;
            IsLoading = false;
            NotifyStateChanged();
        }
        
        public void FinishLoading()
        {
            Console.WriteLine("--- CurrentUserState: FinishLoading LLAMADO ---");
            IsLoading = false;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() 
        {
            Console.WriteLine("--- CurrentUserState: OnChange DISPARADO ---");
            OnChange?.Invoke();
        }

        private IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var claims = new List<Claim>();
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
            if (keyValuePairs != null)
            {
                claims.AddRange(keyValuePairs.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString()!)));
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