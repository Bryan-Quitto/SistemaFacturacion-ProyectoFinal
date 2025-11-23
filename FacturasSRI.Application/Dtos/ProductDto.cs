using System;
using System.ComponentModel.DataAnnotations;
using FacturasSRI.Domain.Enums;

namespace FacturasSRI.Application.Dtos
{
    public class ProductDto
    {
        public Guid Id { get; set; }

        [Required]
        [StringLength(50)]
        public string CodigoPrincipal { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor que cero.")]
        private decimal _precioVentaUnitario;
        public decimal PrecioVentaUnitario 
        { 
            get => _precioVentaUnitario;
            set => _precioVentaUnitario = Math.Round(value, 2);
        }
        public bool ManejaInventario { get; set; } = true;
        public bool ManejaLotes { get; set; } = true;
        public int StockTotal { get; set; }
        public string CreadoPor { get; set; } = string.Empty;
        public Guid UsuarioIdCreador { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public TipoProducto TipoProducto { get; set; }
        public string Marca { get; set; } = string.Empty;
        public Guid CategoriaId { get; set; }
        public string CategoriaNombre { get; set; } = string.Empty;


    }
}
