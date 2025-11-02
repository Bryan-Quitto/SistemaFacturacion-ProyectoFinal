using FacturasSRI.Domain.Enums;
using System;

namespace FacturasSRI.Domain.Entities
{
    public class Usuario
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public RolUsuario Rol { get; set; }
        public bool EstaActivo { get; set; } = true;
    }
}