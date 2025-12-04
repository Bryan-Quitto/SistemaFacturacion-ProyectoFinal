using FacturasSRI.Application.Dtos.Reports;

namespace FacturasSRI.Application.Interfaces
{
    public interface IReportService
    {
        Task<IEnumerable<VentasPorPeriodoDto>> GetVentasPorPeriodoAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId);
        Task<byte[]> GetVentasPorPeriodoAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId);
        
        Task<IEnumerable<VentasPorProductoDto>> GetVentasPorProductoAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId);
        Task<byte[]> GetVentasPorProductoAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId);
        
        Task<IEnumerable<ClienteActividadDto>> GetActividadClientesAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId);
        Task<byte[]> GetActividadClientesAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId);
        
        Task<IEnumerable<CuentasPorCobrarDto>> GetCuentasPorCobrarAsync(Guid? userId);
        Task<byte[]> GetCuentasPorCobrarAsPdfAsync(Guid? userId);
        
        Task<IEnumerable<NotasDeCreditoReportDto>> GetNotasDeCreditoAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId);
        Task<byte[]> GetNotasDeCreditoAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId);
        
        Task<IEnumerable<StockActualDto>> GetStockActualAsync(Guid? userId, bool hideZeroStock = true);
        Task<byte[]> GetStockActualAsPdfAsync(Guid? userId, bool hideZeroStock = true);

        Task<IEnumerable<MovimientoInventarioDto>> GetMovimientosInventarioAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId);
        Task<byte[]> GetMovimientosInventarioAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId);
        
        Task<IEnumerable<ComprasPorPeriodoDto>> GetComprasPorPeriodoAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId);
        Task<byte[]> GetComprasPorPeriodoAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId);
        
        Task<IEnumerable<ProductoStockMinimoDto>> GetProductosBajoStockMinimoAsync(Guid? userId);
        Task<byte[]> GetProductosBajoStockMinimoAsPdfAsync(Guid? userId);
        
        Task<IEnumerable<AjusteInventarioReportDto>> GetAjustesInventarioAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId);
        Task<byte[]> GetAjustesInventarioAsPdfAsync(DateTime fechaInicio, DateTime fechaFin, Guid? userId);
    }
}