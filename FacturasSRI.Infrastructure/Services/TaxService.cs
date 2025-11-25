using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FacturasSRI.Infrastructure.Services
{
    public class TaxService : ITaxService
    {
        private readonly IDbContextFactory<FacturasSRIDbContext> _contextFactory;

        public TaxService(IDbContextFactory<FacturasSRIDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<TaxDto> CreateTaxAsync(TaxDto taxDto)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            if (await context.Impuestos.AnyAsync(t => t.Nombre == taxDto.Nombre || t.CodigoSRI == taxDto.CodigoSRI))
            {
                throw new InvalidOperationException("Ya existe un impuesto con el mismo nombre o código SRI.");
            }
            var tax = new Impuesto
            {
                Id = Guid.NewGuid(),
                Nombre = taxDto.Nombre,
                CodigoSRI = taxDto.CodigoSRI,
                Porcentaje = taxDto.Porcentaje,
                EstaActivo = taxDto.EstaActivo
            };
            context.Impuestos.Add(tax);
            await context.SaveChangesAsync();
            taxDto.Id = tax.Id;
            return taxDto;
        }

        public async Task DeleteTaxAsync(Guid id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var tax = await context.Impuestos.FindAsync(id);
            if (tax != null)
            {
                tax.EstaActivo = !tax.EstaActivo;
                await context.SaveChangesAsync();
            }
        }

        public async Task<TaxDto?> GetTaxByIdAsync(Guid id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var tax = await context.Impuestos.FindAsync(id);
            if (tax == null || !tax.EstaActivo)
            {
                return null;
            }
            return new TaxDto
            {
                Id = tax.Id,
                Nombre = tax.Nombre,
                CodigoSRI = tax.CodigoSRI,
                Porcentaje = tax.Porcentaje,
                EstaActivo = tax.EstaActivo
            };
        }

        public async Task<List<TaxDto>> GetTaxesAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Impuestos.Select(tax => new TaxDto
            {
                Id = tax.Id,
                Nombre = tax.Nombre,
                CodigoSRI = tax.CodigoSRI,
                Porcentaje = tax.Porcentaje,
                EstaActivo = tax.EstaActivo
            }).ToListAsync();
        }

        public async Task<List<TaxDto>> GetActiveTaxesAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Impuestos.Where(t => t.EstaActivo).Select(tax => new TaxDto
            {
                Id = tax.Id,
                Nombre = tax.Nombre,
                CodigoSRI = tax.CodigoSRI,
                Porcentaje = tax.Porcentaje,
                EstaActivo = tax.EstaActivo
            }).ToListAsync();
        }

        public async Task UpdateTaxAsync(TaxDto taxDto)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var tax = await context.Impuestos.FindAsync(taxDto.Id);
            if (tax != null)
            {
                if (await context.Impuestos.AnyAsync(t => t.Id != tax.Id && (t.Nombre == taxDto.Nombre || t.CodigoSRI == taxDto.CodigoSRI)))
                {
                    throw new InvalidOperationException("Ya existe otro impuesto con el mismo nombre o código SRI.");
                }
                tax.Nombre = taxDto.Nombre;
                tax.CodigoSRI = taxDto.CodigoSRI;
                tax.Porcentaje = taxDto.Porcentaje;
                tax.EstaActivo = taxDto.EstaActivo;
                await context.SaveChangesAsync();
            }
        }
    }
}