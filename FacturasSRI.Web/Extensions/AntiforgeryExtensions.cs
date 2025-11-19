using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FacturasSRI.Web.Extensions
{
    public static class AntiforgeryExtensions
    {
        public static RouteGroupBuilder IgnoreAntiforgeryToken(this RouteGroupBuilder group)
        {
            return group.WithMetadata(new IgnoreAntiforgeryTokenAttribute());
        }

        public static RouteHandlerBuilder IgnoreAntiforgeryToken(this RouteHandlerBuilder builder)
        {
            return builder.WithMetadata(new IgnoreAntiforgeryTokenAttribute());
        }
    }
}