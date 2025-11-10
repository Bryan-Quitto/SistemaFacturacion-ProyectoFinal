using FacturasSRI.Domain.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace FacturasSRI.Application.Dtos
{
    public class CustomerDto
    {
        public Guid Id { get; set; }
        public TipoIdentificacion TipoIdentificacion { get; set; }

        [Required]
        public string NumeroIdentificacion { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string RazonSocial { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Direccion { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public bool EstaActivo { get; set; } = true;
        public string CreadoPor { get; set; } = string.Empty;
        public Guid UsuarioIdCreador { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public Guid? UsuarioModificadorId { get; set; }
        public string? UltimaModificacionPor { get; set; }
    }
}