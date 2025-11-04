namespace FacturasSRI.Web.States
{
    public class CurrentUserState
    {
        public bool IsLoggedIn { get; private set; }
        public string? Role { get; private set; }
        public string? Email { get; private set; }
        public Guid UserId { get; private set; }

        public event Action? OnChange;
        private const string AdminSecretKey = "SUPER_SECRET_KEY_123";
        private static readonly Dictionary<string, string> ValidUsers = new()
        {
            { "user@facturas.com", "user123" },
            { "admin@facturas.com", "admin123" }
        };

        public bool Login(string email, string password, string? adminKey = null)
        {
            if (!ValidUsers.TryGetValue(email.ToLower(), out var correctPassword) || correctPassword != password)
            {
                return false;
            }

            if (adminKey != null)
            {
                if (adminKey == AdminSecretKey)
                {
                    LoginAs("Admin", email);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                LoginAs("General", email);
                return true;
            }
        }
        
        private void LoginAs(string role, string email)
        {
            IsLoggedIn = true;
            Role = role;
            Email = email;
            UserId = (role == "Admin") 
                ? Guid.Parse("00000000-0000-0000-0000-000000000001") 
                : Guid.Parse("00000000-0000-0000-0000-000000000002");
            NotifyStateChanged();
        }

        public void Logout()
        {
            IsLoggedIn = false;
            Role = null;
            Email = null;
            UserId = Guid.Empty;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}