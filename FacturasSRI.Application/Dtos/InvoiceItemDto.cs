using System;
    namespace FacturasSRI.Application.Dtos
    {
    public class InvoiceItemDto
    {
    public Guid ProductoId { get; set; }
    public int Cantidad { get; set; }
    }
}