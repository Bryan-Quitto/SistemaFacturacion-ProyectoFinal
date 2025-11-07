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
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<CuentaPorPagar> CuentasPorPagar { get; set; }
        public DbSet<Empresa> Empresas { get; set; }
        public DbSet<Establecimiento> Establecimientos { get; set; }
        public DbSet<Factura> Facturas { get; set; }
        public DbSet<FacturaDetalle> FacturaDetalles { get; set; }
        public DbSet<FacturaDetalleConsumoLote> FacturaDetalleConsumoLotes { get; set; }
        public DbSet<FacturaSRI> FacturasSRI { get; set; }
        public DbSet<Impuesto> Impuestos { get; set; }
        public DbSet<Lote> Lotes { get; set; }
        public DbSet<NotaDeCredito> NotasDeCredito { get; set; }
        public DbSet<NotaDeCreditoDetalle> NotaDeCreditoDetalles { get; set; }
        public DbSet<NotaDeCreditoSRI> NotasDeCreditoSRI { get; set; }
        public DbSet<Permiso> Permisos { get; set; }
        public DbSet<PrecioEspecial> PreciosEspeciales { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<ProductoComponente> ProductoComponentes { get; set; }
        public DbSet<ProductoImpuesto> ProductoImpuestos { get; set; }
        public DbSet<PuntoEmision> PuntosEmision { get; set; }
        public DbSet<Rol> Roles { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<UsuarioRol> UsuarioRoles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<AjusteInventario>().ToTable("AjustesInventario");
            modelBuilder.Entity<Cliente>().ToTable("Clientes");
            modelBuilder.Entity<CuentaPorPagar>().ToTable("CuentasPorPagar");
            modelBuilder.Entity<Empresa>().ToTable("Empresas");
            modelBuilder.Entity<Establecimiento>().ToTable("Establecimientos");
            modelBuilder.Entity<Factura>().ToTable("Facturas");
            modelBuilder.Entity<FacturaDetalle>().ToTable("FacturaDetalles");
            modelBuilder.Entity<FacturaDetalleConsumoLote>().ToTable("FacturaDetalleConsumoLotes");
            modelBuilder.Entity<Impuesto>().ToTable("Impuestos");
            modelBuilder.Entity<Lote>().ToTable("Lotes");
            modelBuilder.Entity<NotaDeCredito>().ToTable("NotasDeCredito");
            modelBuilder.Entity<NotaDeCreditoDetalle>().ToTable("NotaDeCreditoDetalles");
            modelBuilder.Entity<Permiso>().ToTable("Permisos");
            modelBuilder.Entity<PrecioEspecial>().ToTable("PreciosEspeciales");
            modelBuilder.Entity<Producto>().ToTable("Productos");
            modelBuilder.Entity<PuntoEmision>().ToTable("PuntosEmision");
            modelBuilder.Entity<Rol>().ToTable("Roles");
            modelBuilder.Entity<Usuario>().ToTable("Usuarios");

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(FacturasSRIDbContext).Assembly);

            var rolVendedorId = Guid.Parse("e2a87c46-e5b3-4f9e-8c6e-1f2a3b4c5d6e");
            var rolBodegueroId = Guid.Parse("f5b8c9d0-1a2b-3c4d-5e6f-7a8b9c0d1e2f");

            var permGestionarClientes = new Permiso { Id = Guid.Parse("01f6f2c8-2a1e-4c74-8a8f-b9d5c6a1b2d3"), Nombre = "gestionar-clientes", Descripcion = "Acceso a la gestión de clientes" };
            var permCrearFacturas = new Permiso { Id = Guid.Parse("b2d0b8c0-3e7a-4f5c-8b1d-9e6a0c7f1a2b"), Nombre = "crear-facturas", Descripcion = "Acceso a la creación de facturas" };
            var permVerProductos = new Permiso { Id = Guid.Parse("c3e1a9d0-4b8f-4e6c-9a1d-8f7b0c6e2d1a"), Nombre = "ver-productos", Descripcion = "Acceso para ver productos" };
            var permGestionarInventario = new Permiso { Id = Guid.Parse("d4f2b8a1-5c9e-4a6b-8c1d-7e6f0a5b3c2d"), Nombre = "gestionar-inventario", Descripcion = "Acceso a la gestión de inventario y lotes" };
            var permGestionarProductos = new Permiso { Id = Guid.Parse("e5a3c7b2-6d0f-4b8a-9d2e-6f5b1c4a3e1f"), Nombre = "gestionar-productos", Descripcion = "Acceso a la gestión de productos" };
            
            modelBuilder.Entity<Permiso>().HasData(
                permGestionarClientes,
                permCrearFacturas,
                permVerProductos,
                permGestionarInventario,
                permGestionarProductos
            );
            
            modelBuilder.Entity<Rol>()
                .HasMany(r => r.Permisos)
                .WithMany(p => p.Roles)
                .UsingEntity<Dictionary<string, object>>(
                    "RolPermisos",
                    j => j.HasOne<Permiso>().WithMany().HasForeignKey("PermisosId"),
                    j => j.HasOne<Rol>().WithMany().HasForeignKey("RolesId"),
                    j =>
                    {
                        j.HasKey("PermisosId", "RolesId");
                        j.HasData(
                            new { PermisosId = permGestionarClientes.Id, RolesId = rolVendedorId },
                            new { PermisosId = permCrearFacturas.Id, RolesId = rolVendedorId },
                            new { PermisosId = permVerProductos.Id, RolesId = rolVendedorId },

                            new { PermisosId = permGestionarInventario.Id, RolesId = rolBodegueroId },
                            new { PermisosId = permGestionarProductos.Id, RolesId = rolBodegueroId },
                            new { PermisosId = permVerProductos.Id, RolesId = rolBodegueroId }
                        );
                    }
                );
        }
    }
}