// En: FacturasSRI.Infrastructure/Persistence/Configurations/ProductoConfiguration.cs

using FacturasSRI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FacturasSRI.Infrastructure.Persistence.Configurations
{
    public class ProductoConfiguration : IEntityTypeConfiguration<Producto>
    {
        public void Configure(EntityTypeBuilder<Producto> builder)
        {
            builder.HasIndex(p => p.CodigoPrincipal)
                   .IsUnique();

            builder.HasIndex(p => p.Nombre)
                   .IsUnique();
            
            builder.Property(p => p.Marca)
                   .HasMaxLength(150); // Opcional: define un largo máximo para la marca.

            builder.HasOne(p => p.Categoria)
                   .WithMany(c => c.Productos) // Una Categoría tiene muchos Productos.
                   .HasForeignKey(p => p.CategoriaId) // La llave foránea en Producto es CategoriaId.
                   .OnDelete(DeleteBehavior.Restrict); // Impide eliminar una categoría si tiene productos asociados.
        }
    }
}