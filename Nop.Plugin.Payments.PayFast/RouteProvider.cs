using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;
using Microsoft.AspNetCore.Builder;

namespace Nop.Plugin.Payments.PayFast
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            routeBuilder.MapRoute("Plugin.Payments.PayFast.PaymentResult",
                 "Plugins/PaymentPayFast/PaymentResult",
                 new { controller = "PaymentPayFast", action = "PayFastResultHandler" });
        }

        public int Priority
        {
            get { return 0; }
        }
    }
}
