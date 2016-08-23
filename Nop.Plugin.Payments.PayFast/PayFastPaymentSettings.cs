using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.PayFast
{
    public class PayFastPaymentSettings : ISettings
    {
        /// <summary>
        /// Gets or sets merchant ID
        /// </summary>
        public string MerchantId { get; set; }

        /// <summary>
        /// Gets or sets merchant key
        /// </summary>
        public string MerchantKey { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use sandbox (testing environment)
        /// </summary>
        public bool UseSandbox { get; set; }

        /// <summary>
        /// Gets or sets an additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }
    }
}
