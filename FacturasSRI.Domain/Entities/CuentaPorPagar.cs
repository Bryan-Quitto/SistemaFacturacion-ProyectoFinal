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

        // Relación opcional con Lote
        public Guid? LoteId { get; set; }
        [ForeignKey("LoteId")]
        public virtual Lote? Lote { get; set; }

        [Required]
        [MaxLength(200)]
        public string NombreProveedor { get; set; } = string.Empty;

        public string? FacturaCompraPath { get; set; }
        public string? ComprobantePagoPath { get; set; }
        public string? NotaCreditoPath { get; set; }

        public DateTime FechaEmision { get; set; }
        public DateTime? FechaVencimiento { get; set; } 
        public DateTime? FechaPago { get; set; } 

        [Column(TypeName = "decimal(18,2)")]
        public decimal MontoTotal { get; set; }

        public int Cantidad { get; set; }

        public EstadoCompra Estado { get; set; }

        public FormaDePago FormaDePago { get; set; }

        public Guid UsuarioIdCreador { get; set; }

        [ForeignKey("UsuarioIdCreador")]
        public virtual Usuario UsuarioCreador { get; set; } = null!;

        public DateTime FechaCreacion { get; set; }

        public string? NumeroFacturaProveedor { get; set; }
        public int NumeroCompraInterno { get; set; }
    }
}