using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.PayFast
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute("Plugin.Payments.PayFast.PaymentResult",
                 "Plugins/PaymentPayFast/PaymentResult",
                 new { controller = "PaymentPayFast", action = "PayFastResultHandler" },
                 new[] { "Nop.Plugin.Payments.PayFast.Controllers" }
            );
        }

        public int Priority
        {
            get { return 0; }
        }
    }
}
