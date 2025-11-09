using System;
using System.Collections.Generic;

namespace FacturasSRI.Application.Dtos
{
    public class ProductStockDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int TotalStock { get; set; }
        public bool ManejaLotes { get; set; }
        public List<LoteDto> Lotes { get; set; } = new List<LoteDto>();
    }
}