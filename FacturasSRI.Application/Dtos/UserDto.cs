using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FacturasSRI.Application.Dtos
{
    public class UserDto
    {
        public Guid Id { get; set; }

        [Required]
        public string PrimerNombre { get; set; } = string.Empty;
        public string? SegundoNombre { get; set; }

        [Required]
        public string PrimerApellido { get; set; } = string.Empty;
        public string? SegundoApellido { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string? Password { get; set; }
        public bool EstaActivo { get; set; } = true;

        public List<Guid> RolesId { get; set; } = new List<Guid>();
        public List<string> Roles { get; set; } = new List<string>();
    }
}
