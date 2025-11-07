using FacturasSRI.Application.Dtos;
using System;
using System.Threading.Tasks;

namespace FacturasSRI.Application.Interfaces
{
    public interface IPurchaseService
    {
        Task<bool> CreatePurchaseAsync(PurchaseDto purchase, Guid userId);
    }
}