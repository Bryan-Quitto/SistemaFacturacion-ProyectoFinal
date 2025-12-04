using FacturasSRI.Application.Dtos;
using System;
using System.Threading.Tasks;

namespace FacturasSRI.Application.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardStatsDto> GetDashboardStatsAsync(Guid userId, bool isAdmin, bool isBodeguero);
    }
}