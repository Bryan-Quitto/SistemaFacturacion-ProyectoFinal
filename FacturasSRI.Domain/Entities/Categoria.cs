using System;
using System.Collections.Generic;

namespace FacturasSRI.Domain.Entities
{
    public class Categoria
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        // Relación inversa: una categoría puede tener muchos productos
        public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();
    }
}