using System;

namespace FacturasSRI.Application.Dtos
{
    public class ProductDto
    {
        public Guid Id { get; set; }
        public string CodigoPrincipal { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public decimal PrecioVentaUnitario { get; set; }
        public bool ManejaInventario { get; set; } = true;
        public bool ManejaLotes { get; set; } = true;
        public int StockTotal { get; set; }
    }
}