using System;
using System.Collections.Generic;
using FacturasSRI.Domain.Enums;

namespace FacturasSRI.Domain.Entities
{
    public class Producto
    {
        public Guid Id { get; set; }
        public string CodigoPrincipal { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public decimal PrecioVentaUnitario { get; set; }
        public bool ManejaInventario { get; set; } = true;
        public bool ManejaLotes { get; set; } = true;
        public int StockTotal { get; set; }
        public bool EstaActivo { get; set; } = true;
        public Guid UsuarioIdCreador { get; set;}
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public TipoProducto TipoProducto { get; set; }

        public string Marca { get; set; } = string.Empty; // Campo para la marca
        public Guid CategoriaId { get; set; } // Llave foránea para Categoria
        public virtual Categoria Categoria { get; set; } // Propiedad de navegación

        public virtual ICollection<Lote> Lotes { get; set; } = new List<Lote>();
        public virtual ICollection<ProductoImpuesto> ProductoImpuestos { get; set; } = new List<ProductoImpuesto>();
    }
}