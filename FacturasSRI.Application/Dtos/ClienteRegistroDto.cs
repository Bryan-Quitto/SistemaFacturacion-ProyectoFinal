using FacturasSRI.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FacturasSRI.Application.Dtos
{
    public class ClienteRegistroDto
    {
        [Required]
        public TipoIdentificacion TipoIdentificacion { get; set; }

        [Required]
        [StringLength(20)]
        public string NumeroIdentificacion { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string RazonSocial { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Direccion { get; set; } = string.Empty;

        [Required]
        public string Telefono { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Compare("Password", ErrorMessage = "La contraseña y la contraseña de confirmación no coinciden.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
