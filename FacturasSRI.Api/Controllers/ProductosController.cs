using FacturasSRI.Application.Dtos.Productos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FacturasSRI.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductosController : ControllerBase
    {
        private readonly IProductoRepository _productoRepository;

        public ProductosController(IProductoRepository productoRepository)
        {
            _productoRepository = productoRepository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductoDto>>> GetAll()
        {
            var productos = await _productoRepository.GetAllProductsAsync();
            return Ok(productos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Producto>> GetById(Guid id)
        {
            var producto = await _productoRepository.GetProductByIdAsync(id);
            if (producto == null)
            {
                return NotFound();
            }
            return Ok(producto);
        }

        [HttpPost]
        public async Task<ActionResult<Producto>> Create([FromBody] CreateProductoDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var nuevoProducto = new Producto
            {
                Id = Guid.NewGuid(),
                CodigoPrincipal = dto.CodigoPrincipal,
                Nombre = dto.Nombre,
                Descripcion = dto.Descripcion,
                PrecioVentaUnitario = dto.PrecioVentaUnitario,
                TipoImpuestoIVA = dto.TipoImpuestoIVA,
                Tipo = TipoProducto.Simple, // Asignado por defecto
                EstaActivo = true,
                FechaCreacion = DateTime.UtcNow,
                // TODO: Reemplazar este GUID quemado por el ID del usuario autenticado
                UsuarioIdCreador = Guid.Parse("00000000-0000-0000-0000-000000000001") 
            };

            await _productoRepository.CreateProductAsync(nuevoProducto);

            return CreatedAtAction(nameof(GetById), new { id = nuevoProducto.Id }, nuevoProducto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductoDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var productoExistente = await _productoRepository.GetProductByIdAsync(id);
            if (productoExistente == null)
            {
                return NotFound();
            }

            productoExistente.Nombre = dto.Nombre;
            productoExistente.Descripcion = dto.Descripcion;
            productoExistente.PrecioVentaUnitario = dto.PrecioVentaUnitario;

            await _productoRepository.UpdateProductAsync(productoExistente);

            return NoContent(); 
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var producto = await _productoRepository.GetProductByIdAsync(id);
            if (producto == null)
            {
                return NotFound();
            }

            await _productoRepository.DeactivateProductAsync(producto);
            return NoContent();
        }
    }
}