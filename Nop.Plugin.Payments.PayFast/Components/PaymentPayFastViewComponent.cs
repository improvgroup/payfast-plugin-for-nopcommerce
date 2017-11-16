using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.PayFast.Components
{
    [ViewComponent(Name = "PaymentPayFast")]
    public class PaymentPayFastViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.PayFast/Views/PaymentInfo.cshtml");
        }
    }
}
