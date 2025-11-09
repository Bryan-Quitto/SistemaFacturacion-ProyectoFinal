using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Domain.Enums;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FacturasSRI.Infrastructure.Services
{
    public class AjusteInventarioService : IAjusteInventarioService
    {
        private readonly FacturasSRIDbContext _context;

        public AjusteInventarioService(FacturasSRIDbContext context)
        {
            _context = context;
        }

        public async Task<List<AjusteListItemDto>> GetAdjustmentsAsync()
        {
            var query = from ajuste in _context.AjustesInventario
                        join producto in _context.Productos on ajuste.ProductoId equals producto.Id into productoJoin
                        from producto in productoJoin.DefaultIfEmpty()
                        join usuario in _context.Usuarios on ajuste.UsuarioIdAutoriza equals usuario.Id into usuarioJoin
                        from usuario in usuarioJoin.DefaultIfEmpty()
                        orderby ajuste.Fecha descending
                        select new AjusteListItemDto
                        {
                            Fecha = ajuste.Fecha,
                            ProductoNombre = producto != null ? producto.Nombre : "[Producto Eliminado]",
                            Tipo = ajuste.Tipo,
                            CantidadAjustada = ajuste.CantidadAjustada,
                            Motivo = ajuste.Motivo,
                            UsuarioAutoriza = usuario != null ? usuario.PrimerNombre + " " + usuario.PrimerApellido : "Usuario no encontrado"
                        };
            
            return await query.ToListAsync();
        }

        public async Task CreateAdjustmentAsync(AjusteInventarioDto ajusteDto)
        {
            var producto = await _context.Productos.FindAsync(ajusteDto.ProductoId);
            if (producto == null)
            {
                throw new InvalidOperationException("El producto especificado no existe.");
            }

            var ajuste = new AjusteInventario
            {
                Id = Guid.NewGuid(),
                ProductoId = ajusteDto.ProductoId,
                LoteId = ajusteDto.LoteId,
                CantidadAjustada = ajusteDto.CantidadAjustada,
                Tipo = ajusteDto.Tipo,
                Motivo = ajusteDto.Motivo,
                Fecha = DateTime.UtcNow,
                UsuarioIdAutoriza = ajusteDto.UsuarioIdAutoriza
            };

            if (producto.ManejaLotes)
            {
                if (!ajusteDto.LoteId.HasValue)
                {
                    throw new InvalidOperationException("Debe seleccionar un lote para productos que manejan lotes.");
                }
                
                var lote = await _context.Lotes.FindAsync(ajusteDto.LoteId.Value);
                if (lote == null)
                {
                    throw new InvalidOperationException("El lote especificado no existe.");
                }
                AjustarStock(lote, ajusteDto.CantidadAjustada, ajusteDto.Tipo);
            }
            else
            {
                AjustarStock(producto, ajusteDto.CantidadAjustada, ajusteDto.Tipo);
            }

            _context.AjustesInventario.Add(ajuste);
            await _context.SaveChangesAsync();
        }

        private void AjustarStock(Lote lote, int cantidad, TipoAjusteInventario tipo)
        {
            switch (tipo)
            {
                case TipoAjusteInventario.Daño:
                case TipoAjusteInventario.Perdida:
                    if (lote.CantidadDisponible < cantidad)
                    {
                        throw new InvalidOperationException($"No se puede reducir el stock en {cantidad}. Stock disponible en el lote: {lote.CantidadDisponible}.");
                    }
                    lote.CantidadDisponible -= cantidad;
                    break;
                
                case TipoAjusteInventario.Conteo:
                case TipoAjusteInventario.Inicial:
                case TipoAjusteInventario.Otro:
                    lote.CantidadDisponible += cantidad;
                    break;
            }
        }

        private void AjustarStock(Producto producto, int cantidad, TipoAjusteInventario tipo)
        {
            switch (tipo)
            {
                case TipoAjusteInventario.Daño:
                case TipoAjusteInventario.Perdida:
                    if (producto.StockTotal < cantidad)
                    {
                        throw new InvalidOperationException($"No se puede reducir el stock en {cantidad}. Stock total disponible: {producto.StockTotal}.");
                    }
                    producto.StockTotal -= cantidad;
                    break;
                
                case TipoAjusteInventario.Conteo:
                case TipoAjusteInventario.Inicial:
                case TipoAjusteInventario.Otro:
                    producto.StockTotal += cantidad;
                    break;
            }
        }
    }
}