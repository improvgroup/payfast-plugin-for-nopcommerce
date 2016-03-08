using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.PayFast
{
    public class PayFastPaymentSettings : ISettings
    {
        public string Url { get; set; }
        public string ValidateUrl { get; set; }
        public string merchant_id { get; set; }
        public string merchant_key { get; set; }
       
    }
}
