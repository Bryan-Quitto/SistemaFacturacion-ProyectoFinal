using FacturasSRI.Application.Dtos;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FacturasSRI.Application.Interfaces
{
    public interface IPurchaseService
    {
        Task<bool> CreatePurchaseAsync(PurchaseDto purchase);
        Task<List<PurchaseListItemDto>> GetPurchasesAsync();
        Task<PurchaseListItemDto?> GetPurchaseByIdAsync(Guid id);
        Task<bool> RegisterPaymentAsync(RegisterPaymentDto paymentDto, Stream fileStream, string fileName);
        Task MarcarComprasVencidasAsync();
        
        // MODIFICADO: Ahora requiere el archivo de la Nota de Crédito
        Task AnularCompraAsync(Guid compraId, Guid usuarioId, Stream notaCreditoStream, string notaCreditoFileName);
    }
}