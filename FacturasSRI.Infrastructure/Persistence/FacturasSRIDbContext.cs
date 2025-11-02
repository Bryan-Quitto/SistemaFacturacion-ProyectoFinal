using Microsoft.EntityFrameworkCore;
using FacturasSRI.Domain.Entities;
using System.Reflection;

namespace FacturasSRI.Infrastructure.Persistence
{
    public class FacturasSRIDbContext : DbContext
    {
        public FacturasSRIDbContext(DbContextOptions<FacturasSRIDbContext> options) : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<PrecioEspecial> PreciosEspeciales { get; set; }
        public DbSet<ProductoComponente> ProductoComponentes { get; set; }
        public DbSet<Lote> Lotes { get; set; }
        public DbSet<AjusteInventario> AjustesInventario { get; set; }
        public DbSet<Factura> Facturas { get; set; }
        public DbSet<FacturaDetalle> FacturaDetalles { get; set; }
        public DbSet<FacturaDetalleConsumoLote> FacturaDetalleConsumoLotes { get; set; }
        public DbSet<NotaDeCredito> NotasDeCredito { get; set; }
        public DbSet<NotaDeCreditoDetalle> NotaDeCreditoDetalles { get; set; }
        public DbSet<FacturaSRI> FacturasSRI { get; set; }
        public DbSet<NotaDeCreditoSRI> NotasDeCreditoSRI { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            modelBuilder.Entity<FacturaSRI>().HasKey(fsri => fsri.FacturaId);
            modelBuilder.Entity<NotaDeCreditoSRI>().HasKey(ncsri => ncsri.NotaDeCreditoId);
            
            modelBuilder.Entity<Factura>()
                .HasOne(f => f.InformacionSRI)
                .WithOne(fsri => fsri.Factura)
                .HasForeignKey<FacturaSRI>(fsri => fsri.FacturaId);

            modelBuilder.Entity<NotaDeCredito>()
                .HasOne(nc => nc.InformacionSRI)
                .WithOne(ncsri => ncsri.NotaDeCredito)
                .HasForeignKey<NotaDeCreditoSRI>(ncsri => ncsri.NotaDeCreditoId);

            modelBuilder.Entity<ProductoComponente>()
                .HasOne(pc => pc.ProductoKit)
                .WithMany(p => p.Componentes)
                .HasForeignKey(pc => pc.ProductoKitId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProductoComponente>()
                .HasOne(pc => pc.ProductoComponenteItem)
                .WithMany()
                .HasForeignKey(pc => pc.ProductoComponenteId)
                .OnDelete(DeleteBehavior.Restrict);

            foreach (var property in modelBuilder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(18, 2)");
            }
        }
    }
}