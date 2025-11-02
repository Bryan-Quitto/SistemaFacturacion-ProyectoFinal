using FacturasSRI.Domain.Enums;
using System;
using System.Collections.Generic;

namespace FacturasSRI.Domain.Entities
{
    public class Cliente
    {
        public Guid Id { get; set; }
        public TipoIdentificacion TipoIdentificacion { get; set; }
        public string NumeroIdentificacion { get; set; } = string.Empty;
        public string RazonSocial { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public bool EstaActivo { get; set; } = true;
        public Guid UsuarioIdCreador { get; set; }
        public DateTime FechaCreacion { get; set; }
        public virtual ICollection<Factura> Facturas { get; set; } = new List<Factura>();
        public virtual ICollection<NotaDeCredito> NotasDeCredito { get; set; } = new List<NotaDeCredito>();
    }
}