namespace FacturasSRI.Application.Dtos
{
    // A unified view model for the MyProfile page to handle both Users and Customers
    public class ProfileViewModel
    {
        public string UserType { get; set; } = string.Empty; // "User" or "Customer"
        
        // Common Fields
        public string Email { get; set; } = string.Empty;

        // User-specific fields
        public string? PrimerNombre { get; set; }
        public string? SegundoNombre { get; set; }
        public string? PrimerApellido { get; set; }
        public string? SegundoApellido { get; set; }

        // Customer-specific fields
        public string? RazonSocial { get; set; }
        public string? Direccion { get; set; }
        public string? Telefono { get; set; }
    }
}
