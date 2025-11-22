using System;
using System.Collections.Generic;
using FacturasSRI.Domain.Enums;

namespace FacturasSRI.Application.Dtos
{
    public class CreateInvoiceDto
    {
        public Guid? ClienteId { get; set; } // Made nullable
        public Guid UsuarioIdCreador { get; set; }
        public List<InvoiceItemDto> Items { get; set; } = new();

        // Payment details
        public FormaDePago FormaDePago { get; set; }
        public int? DiasCredito { get; set; }
        public decimal MontoAbonoInicial { get; set; }

        public bool EsConsumidorFinal { get; set; }

        // Optional fields for new customers
        public TipoIdentificacion? TipoIdentificacionComprador { get; set; }
        public string? RazonSocialComprador { get; set; }
        public string? IdentificacionComprador { get; set; }
        public string? DireccionComprador { get; set; }
        public string? EmailComprador { get; set; }

        public bool EsBorrador { get; set; }
    }
}