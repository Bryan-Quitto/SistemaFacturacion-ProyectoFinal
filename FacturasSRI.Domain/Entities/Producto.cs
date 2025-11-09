using System;
using System.Collections.Generic;

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

        public virtual ICollection<Lote> Lotes { get; set; } = new List<Lote>();
        public virtual ICollection<ProductoImpuesto> ProductoImpuestos { get; set; } = new List<ProductoImpuesto>();
    }
}