// En: FacturasSRI.Infrastructure/Persistence/Configurations/CategoriaConfiguration.cs

using FacturasSRI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace FacturasSRI.Infrastructure.Persistence.Configurations
{
    public class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
    {
        public void Configure(EntityTypeBuilder<Categoria> builder)
        {
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Nombre).IsRequired().HasMaxLength(100);

            // Semillero de datos (Seed data)
            builder.HasData(
                new Categoria { Id = Guid.NewGuid(), Nombre = "Electrónica" },
                new Categoria { Id = Guid.NewGuid(), Nombre = "Ropa y Accesorios" },
                new Categoria { Id = Guid.NewGuid(), Nombre = "Alimentos y Bebidas" },
                new Categoria { Id = Guid.NewGuid(), Nombre = "Hogar y Jardín" },
                new Categoria { Id = Guid.NewGuid(), Nombre = "Salud y Belleza" }
            );
        }
    }
}