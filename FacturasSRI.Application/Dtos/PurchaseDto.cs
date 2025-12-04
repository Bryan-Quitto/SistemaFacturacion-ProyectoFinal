using System;
using System.ComponentModel.DataAnnotations;
using FacturasSRI.Domain.Enums;

namespace FacturasSRI.Application.Dtos
{
    public class PurchaseDto
    {
        [Required]
        public Guid ProductoId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
        public int Cantidad { get; set; }

        [Required(ErrorMessage = "El precio de compra es obligatorio.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio de compra debe ser mayor que cero.")]
        public decimal PrecioCompraUnitario { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un impuesto.")]
        public Guid ImpuestoId { get; set; }

        public DateTime? FechaCaducidad { get; set; }
        
        [Required]
        public string NombreProveedor { get; set; } = string.Empty;
        
        public string? FacturaCompraPath { get; set; }

        public Guid UsuarioIdCreador { get; set; }

        // Campo para la forma de pago (Contado/Crédito)
        public FormaDePago FormaDePago { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        
        [Display(Name = "Nº Factura Proveedor")]
        public string? NumeroFacturaProveedor { get; set; } 
    }
}