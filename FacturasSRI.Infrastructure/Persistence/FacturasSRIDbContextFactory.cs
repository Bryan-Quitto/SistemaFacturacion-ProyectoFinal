using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace FacturasSRI.Infrastructure.Persistence
{
    public class FacturasSRIDbContextFactory : IDesignTimeDbContextFactory<FacturasSRIDbContext>
    {
        public FacturasSRIDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<FacturasSRIDbContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            optionsBuilder.UseNpgsql(connectionString);

            return new FacturasSRIDbContext(optionsBuilder.Options);
        }
    }
}