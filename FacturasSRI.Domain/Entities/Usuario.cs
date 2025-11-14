using System;
using System.Collections.Generic;

namespace FacturasSRI.Domain.Entities
{
    public class Usuario
    {
        public Guid Id { get; set; }
        public string PrimerNombre { get; set; } = string.Empty;
        public string? SegundoNombre { get; set; }
        public string PrimerApellido { get; set; } = string.Empty;
        public string? SegundoApellido { get; set; }
        public string Email { get; set; } = string.Empty;
                public string PasswordHash { get; set; } = string.Empty;
                public bool EstaActivo { get; set; } = true;
                public DateTime FechaCreacion { get; set; }
                public DateTime? FechaModificacion { get; set; }
                public string? PasswordResetToken { get; set; }
                public DateTime? PasswordResetTokenExpiry { get; set; }
        
                public virtual ICollection<UsuarioRol> UsuarioRoles { get; set; } = new List<UsuarioRol>();
            }
        }
        