using FacturasSRI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FacturasSRI.Infrastructure.Persistence.Configurations
{
    public class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
    {
        public void Configure(EntityTypeBuilder<Categoria> builder)
        {
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Nombre).IsRequired().HasMaxLength(100);

            // Categorías tecnológicas para Aether Tech
           /* 
           builder.HasData(
                new Categoria { Id = Guid.NewGuid(), Nombre = "Computadores y Laptops" },
                new Categoria { Id = Guid.NewGuid(), Nombre = "Componentes de PC" }, // RAM, Discos, Procesadores
                new Categoria { Id = Guid.NewGuid(), Nombre = "Periféricos" }, // Teclados, Mouse, Monitores
                new Categoria { Id = Guid.NewGuid(), Nombre = "Redes y Conectividad" }, // Routers, Cables, Switch
                new Categoria { Id = Guid.NewGuid(), Nombre = "Impresión y Suministros" },
                new Categoria { Id = Guid.NewGuid(), Nombre = "Software y Licencias" },
                new Categoria { Id = Guid.NewGuid(), Nombre = "Servidores y Enterprise" },
                new Categoria { Id = Guid.NewGuid(), Nombre = "Accesorios y Cables" }
            );
            */
        }
    }
}