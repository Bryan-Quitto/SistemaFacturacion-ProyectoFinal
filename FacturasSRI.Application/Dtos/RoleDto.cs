using System;
using System.ComponentModel.DataAnnotations;

namespace FacturasSRI.Application.Dtos
{
    public class RoleDto
    {
        public Guid Id { get; set; }

        [Required]
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public bool EstaActivo { get; set; } = true;
    }
}