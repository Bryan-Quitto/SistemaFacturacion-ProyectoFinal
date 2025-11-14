using System.ComponentModel.DataAnnotations;

namespace FacturasSRI.Application.Dtos
{
    public class UpdateProfileDto
    {
        [Required(ErrorMessage = "El primer nombre es requerido.")]
        public string PrimerNombre { get; set; } = string.Empty;
        
        public string? SegundoNombre { get; set; }

        [Required(ErrorMessage = "El primer apellido es requerido.")]
        public string PrimerApellido { get; set; } = string.Empty;

        public string? SegundoApellido { get; set; }
    }
}
