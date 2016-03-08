using System.ComponentModel;
using System.Web.Mvc;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.PayFast.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        [DisplayName("Url")]
        public string Url { get; set; }

        [DisplayName("Validate Url")]
        public string ValidateUrl { get; set; }
        
        [DisplayName("Merchant ID")]
        public string merchant_id { get; set; }

        [DisplayName("Merchant Key")]
        public string merchant_key { get; set; }
    }
}