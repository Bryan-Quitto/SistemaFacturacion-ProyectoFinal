using FacturasSRI.Application.Dtos.Clientes;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FacturasSRI.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientesController : ControllerBase
    {
        private readonly IClienteRepository _clienteRepository;

        public ClientesController(IClienteRepository clienteRepository)
        {
            _clienteRepository = clienteRepository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ClienteDto>>> GetAll()
        {
            var clientes = await _clienteRepository.GetAllAsync();
            var clienteDtos = new List<ClienteDto>();
            foreach (var cliente in clientes)
            {
                clienteDtos.Add(new ClienteDto
                {
                    Id = cliente.Id,
                    TipoIdentificacion = (int)cliente.TipoIdentificacion,
                    NumeroIdentificacion = cliente.NumeroIdentificacion,
                    RazonSocial = cliente.RazonSocial,
                    Email = cliente.Email,
                    Direccion = cliente.Direccion,
                    Telefono = cliente.Telefono,
                    EstaActivo = cliente.EstaActivo
                });
            }
            return Ok(clienteDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ClienteDto>> GetById(Guid id)
        {
            var cliente = await _clienteRepository.GetByIdAsync(id);
            if (cliente == null)
            {
                return NotFound();
            }

            var clienteDto = new ClienteDto
            {
                Id = cliente.Id,
                TipoIdentificacion = (int)cliente.TipoIdentificacion,
                NumeroIdentificacion = cliente.NumeroIdentificacion,
                RazonSocial = cliente.RazonSocial,
                Email = cliente.Email,
                Direccion = cliente.Direccion,
                Telefono = cliente.Telefono,
                EstaActivo = cliente.EstaActivo
            };

            return Ok(clienteDto);
        }

        [HttpPost]
        public async Task<ActionResult<ClienteDto>> Create(CreateClienteDto createClienteDto)
        {
            var cliente = new Cliente
            {
                TipoIdentificacion = (Domain.Enums.TipoIdentificacion)createClienteDto.TipoIdentificacion,
                NumeroIdentificacion = createClienteDto.NumeroIdentificacion,
                RazonSocial = createClienteDto.RazonSocial,
                // Domain `Cliente` properties are non-nullable strings; coalesce nullable DTO fields to empty string
                Email = createClienteDto.Email ?? string.Empty,
                Direccion = createClienteDto.Direccion ?? string.Empty,
                Telefono = createClienteDto.Telefono ?? string.Empty,
                UsuarioIdCreador = Guid.NewGuid()
            };

            var nuevoCliente = await _clienteRepository.AddAsync(cliente);

            var clienteDto = new ClienteDto
            {
                Id = nuevoCliente.Id,
                TipoIdentificacion = (int)nuevoCliente.TipoIdentificacion,
                NumeroIdentificacion = nuevoCliente.NumeroIdentificacion,
                RazonSocial = nuevoCliente.RazonSocial,
                Email = nuevoCliente.Email,
                Direccion = nuevoCliente.Direccion,
                Telefono = nuevoCliente.Telefono,
                EstaActivo = nuevoCliente.EstaActivo
            };

            return CreatedAtAction(nameof(GetById), new { id = nuevoCliente.Id }, clienteDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, UpdateClienteDto updateClienteDto)
        {
            var clienteExistente = await _clienteRepository.GetByIdAsync(id);

            if (clienteExistente == null)
            {
                return NotFound();
            }

            // Map and convert types safely
            clienteExistente.TipoIdentificacion = (Domain.Enums.TipoIdentificacion)updateClienteDto.TipoIdentificacion;
            clienteExistente.NumeroIdentificacion = updateClienteDto.NumeroIdentificacion;
            clienteExistente.RazonSocial = updateClienteDto.RazonSocial;
            clienteExistente.Email = updateClienteDto.Email ?? string.Empty;
            clienteExistente.Direccion = updateClienteDto.Direccion ?? string.Empty;
            clienteExistente.Telefono = updateClienteDto.Telefono ?? string.Empty;

            await _clienteRepository.UpdateAsync(clienteExistente);

            return NoContent();
        }

        [HttpPatch("{id}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var clienteExistente = await _clienteRepository.GetByIdAsync(id);
            if (clienteExistente == null)
            {
                return NotFound();
            }

            await _clienteRepository.DeactivateAsync(id);
            return NoContent();
        }
    }
}