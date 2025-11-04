using FacturasSRI.Application.Dtos.Inventario;
using FacturasSRI.Infrastructure.Persistence;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace FacturasSRI.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventarioController : ControllerBase
    {
        private readonly ILoteRepository _loteRepository;

        public InventarioController(ILoteRepository loteRepository)
        {
            _loteRepository = loteRepository;
        }

        [HttpGet("lotes")]
        public async Task<IActionResult> GetAll()
        {
            var lotes = await _loteRepository.GetAllAsync();
            var loteDtos = lotes.Select(l => new LoteDto
            {
                Id = l.Id,
                ProductoId = l.ProductoId,
                CantidadComprada = l.CantidadComprada,
                CantidadDisponible = l.CantidadDisponible,
                PrecioCompraUnitario = l.PrecioCompraUnitario,
                FechaCompra = l.FechaCompra,
                FechaCaducidad = l.FechaCaducidad
            });

            return Ok(loteDtos);
        }

        // POST: /api/inventario/compra
        [HttpPost("compra")]
        public async Task<IActionResult> RegistrarCompra([FromBody] CreateLoteDto dto)
        {
            if (dto == null)
                return BadRequest("Payload vacío");

            if (dto.Cantidad <= 0)
                return BadRequest("La cantidad debe ser mayor que cero");

            if (dto.PrecioCompra < 0)
                return BadRequest("El precio no puede ser negativo");

            // Validar que exista el producto a través del repositorio
            var existe = await _loteRepository.ProductoExistsAsync(dto.ProductoId);
            if (!existe)
                return NotFound(new { message = "Producto no encontrado" });

            var now = DateTime.UtcNow;
            var lote = new Lote
            {
                Id = Guid.NewGuid(),
                ProductoId = dto.ProductoId,
                CantidadComprada = dto.Cantidad,
                CantidadDisponible = dto.Cantidad,
                PrecioCompraUnitario = dto.PrecioCompra,
                FechaCompra = dto.FechaCompra ?? now,
                FechaCaducidad = dto.FechaCaducidad,
                UsuarioIdCreador = dto.UsuarioId ?? Guid.Empty,
                FechaCreacion = now
            };

            var nuevo = await _loteRepository.AddAsync(lote);

            var loteDto = new LoteDto
            {
                Id = nuevo.Id,
                ProductoId = nuevo.ProductoId,
                CantidadComprada = nuevo.CantidadComprada,
                CantidadDisponible = nuevo.CantidadDisponible,
                PrecioCompraUnitario = nuevo.PrecioCompraUnitario,
                FechaCompra = nuevo.FechaCompra,
                FechaCaducidad = nuevo.FechaCaducidad
            };

            return CreatedAtAction(nameof(GetLoteById), new { id = lote.Id }, loteDto);
        }

        // Helper: GET lote by id (used by CreatedAtAction)
        [HttpGet("lotes/{id}")]
        public async Task<IActionResult> GetLoteById(Guid id)
        {
            var lote = await _loteRepository.GetByIdAsync(id);
            if (lote == null)
                return NotFound();

            var loteDto = new LoteDto
            {
                Id = lote.Id,
                ProductoId = lote.ProductoId,
                CantidadComprada = lote.CantidadComprada,
                CantidadDisponible = lote.CantidadDisponible,
                PrecioCompraUnitario = lote.PrecioCompraUnitario,
                FechaCompra = lote.FechaCompra,
                FechaCaducidad = lote.FechaCaducidad
            };

            return Ok(loteDto);
        }
    }
}
