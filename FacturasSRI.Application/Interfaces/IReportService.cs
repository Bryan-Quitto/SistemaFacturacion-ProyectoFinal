using FacturasSRI.Application.Dtos.Reports;

namespace FacturasSRI.Application.Interfaces
{
    public interface IReportService
    {
        Task<IEnumerable<VentasPorPeriodoDto>> GetVentasPorPeriodoAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<IEnumerable<VentasPorProductoDto>> GetVentasPorProductoAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<IEnumerable<ClienteActividadDto>> GetActividadClientesAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<IEnumerable<CuentasPorCobrarDto>> GetCuentasPorCobrarAsync();
        Task<IEnumerable<NotasDeCreditoReportDto>> GetNotasDeCreditoAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<IEnumerable<StockActualDto>> GetStockActualAsync();
        Task<IEnumerable<MovimientoInventarioDto>> GetMovimientosInventarioAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<IEnumerable<ComprasPorPeriodoDto>> GetComprasPorPeriodoAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<IEnumerable<ProductoStockMinimoDto>> GetProductosBajoStockMinimoAsync();
        Task<IEnumerable<AjusteInventarioReportDto>> GetAjustesInventarioAsync(DateTime fechaInicio, DateTime fechaFin);
    }
}
