using FacturasSRI.Domain.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FacturasSRI.Domain.Entities
{
    public class CuentaPorPagar
    {
        [Key]
        public Guid Id { get; set; }

        public Guid ProductoId { get; set; }
        
        [ForeignKey("ProductoId")]
        public virtual Producto Producto { get; set; } = null!;

        // Relación opcional con Lote (si el producto maneja lotes)
        public Guid? LoteId { get; set; }
        [ForeignKey("LoteId")]
        public virtual Lote? Lote { get; set; }

        [Required]
        [MaxLength(200)]
        public string NombreProveedor { get; set; } = string.Empty;

        // Ruta del archivo PDF de la factura en el storage
        public string? FacturaCompraPath { get; set; }
        
        // Ruta del archivo PDF del comprobante de pago
        public string? ComprobantePagoPath { get; set; }

        // NUEVO: Ruta del archivo PDF de la Nota de Crédito (Anulación)
        public string? NotaCreditoPath { get; set; }

        public DateTime FechaEmision { get; set; }
        
        public DateTime? FechaVencimiento { get; set; } // Obligatorio solo para Credito
        
        public DateTime? FechaPago { get; set; } // Se llena cuando se paga

        [Column(TypeName = "decimal(18,2)")]
        public decimal MontoTotal { get; set; }

        public int Cantidad { get; set; }

        public EstadoCompra Estado { get; set; }

        public FormaDePago FormaDePago { get; set; }

        // Auditoría
        public Guid UsuarioIdCreador { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}