using System.ComponentModel;
using System.Web.Mvc;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.PayFast.Models
{
    public class PaymentResultModel : BaseNopModel
    {

        [DisplayName("Order ID")]
        public string OrderID { get; set; }

        [DisplayName("Error Message")]
        public string Message { get; set; }
    }
}