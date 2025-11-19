using FacturasSRI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace FacturasSRI.Infrastructure.Persistence
{
    public class FacturasSRIDbContext : DbContext
    {
        public FacturasSRIDbContext(DbContextOptions<FacturasSRIDbContext> options) : base(options)
        {
        }

        public DbSet<AjusteInventario> AjustesInventario { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<CuentaPorCobrar> CuentasPorCobrar { get; set; }
        public DbSet<CuentaPorPagar> CuentasPorPagar { get; set; }
        public DbSet<Factura> Facturas { get; set; }
        public DbSet<FacturaDetalle> FacturaDetalles { get; set; }
        public DbSet<FacturaDetalleConsumoLote> FacturaDetalleConsumoLotes { get; set; }
        public DbSet<FacturaSRI> FacturasSRI { get; set; }
        public DbSet<Impuesto> Impuestos { get; set; }
        public DbSet<Lote> Lotes { get; set; }
        public DbSet<NotaDeCredito> NotasDeCredito { get; set; }
        public DbSet<NotaDeCreditoDetalle> NotaDeCreditoDetalles { get; set; }
        public DbSet<NotaDeCreditoSRI> NotasDeCreditoSRI { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<ProductoImpuesto> ProductoImpuestos { get; set; }
        public DbSet<Rol> Roles { get; set; }
        public DbSet<Secuencial> Secuenciales { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<UsuarioRol> UsuarioRoles { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<AjusteInventario>().ToTable("AjustesInventario");
            modelBuilder.Entity<Cliente>().ToTable("Clientes");
            modelBuilder.Entity<CuentaPorCobrar>().ToTable("CuentasPorCobrar");
            modelBuilder.Entity<CuentaPorPagar>().ToTable("CuentasPorPagar");
            modelBuilder.Entity<Factura>().ToTable("Facturas");
            modelBuilder.Entity<FacturaDetalle>().ToTable("FacturaDetalles");
            modelBuilder.Entity<FacturaDetalleConsumoLote>().ToTable("FacturaDetalleConsumoLotes");
            modelBuilder.Entity<Impuesto>().ToTable("Impuestos");
            modelBuilder.Entity<Lote>().ToTable("Lotes");
            modelBuilder.Entity<NotaDeCredito>().ToTable("NotasDeCredito");
            modelBuilder.Entity<NotaDeCreditoDetalle>().ToTable("NotaDeCreditoDetalles");
            modelBuilder.Entity<Producto>().ToTable("Productos");
            modelBuilder.Entity<Rol>().ToTable("Roles");
            modelBuilder.Entity<Secuencial>().ToTable("Secuenciales");
            modelBuilder.Entity<Usuario>().ToTable("Usuarios");

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(FacturasSRIDbContext).Assembly);
            
            var adminRoleId = new Guid("d3b1b4a9-2f7b-4e6a-9f6b-1c2c3d4e5f6a");
            var vendedorRoleId = new Guid("e2a87c46-e5b3-4f9e-8c6e-1f2a3b4c5d6e");
            var bodegueroRoleId = new Guid("f5b8c9d0-1a2b-3c4d-5e6f-7a8b9c0d1e2f");
            var adminUserId = new Guid("a9b1b4d3-3f7b-4e6a-9f6b-1c2c3d4e5f6b");
            var defaultProveedorId = new Guid("c1d2e3f4-5a6b-7c8d-9e0f-1a2b3c4d5e6f"); // New GUID for default supplier

            // modelBuilder.Entity<Rol>().HasData(
            //     new Rol {
            //         Id = adminRoleId,
            //         Nombre = "Administrador",
            //         Descripcion = "Acceso total al sistema.",
            //         EstaActivo = true
            //     },
            //     new Rol {
            //         Id = vendedorRoleId,
            //         Nombre = "Vendedor",
            //         Descripcion = "Puede gestionar clientes y facturas.",
            //         EstaActivo = true
            //     },
            //     new Rol {
            //         Id = bodegueroRoleId,
            //         Nombre = "Bodeguero",
            //         Descripcion = "Puede gestionar productos, compras e inventario.",
            //         EstaActivo = true
            //     }
            // );
            
            // modelBuilder.Entity<Usuario>().HasData(new Usuario
            // {
            //     Id = adminUserId,
            //     PrimerNombre = "Admin",
            //     PrimerApellido = "Aether",
            //     Email = "admin@facturassri.com",
            //     PasswordHash = "$2a$11$KnYr45JSbCoMg4Jtkg0GXegC7SegKYTidLxFYYljNwtLH0l024qLG",
            //     EstaActivo = true
            // });

            // modelBuilder.Entity<UsuarioRol>().HasData(new UsuarioRol
            // {
            //     UsuarioId = adminUserId,
            //     RolId = adminRoleId
            // });

        }
    }
}