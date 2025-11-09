using FacturasSRI.Application.Dtos;
using System.Threading.Tasks;

namespace FacturasSRI.Application.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardStatsDto> GetDashboardStatsAsync();
    }
}