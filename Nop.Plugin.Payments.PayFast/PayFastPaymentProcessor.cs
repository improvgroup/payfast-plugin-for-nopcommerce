using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.PayFast.Controllers;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework;

namespace Nop.Plugin.Payments.PayFast
{
    /// <summary>
    /// PayFast.co.za payment processor
    /// </summary>
    class PayFastPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly PayFastPaymentSettings _payFastPaymentSettings;
        private readonly ILocalizationService _localizationService;

        #endregion

        #region Ctor

        public PayFastPaymentProcessor(IOrderTotalCalculationService orderTotalCalculationService,
            ISettingService settingService,
            IWebHelper webHelper,
            PayFastPaymentSettings payFastPaymentSettings,
            ILocalizationService localizationService)
        {
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._settingService = settingService;
            this._webHelper = webHelper;
            this._payFastPaymentSettings = payFastPaymentSettings;
            this._localizationService = localizationService;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult();
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var storeLocation = _webHelper.GetStoreLocation();

            var post = new RemotePost
            {
                FormName = "PayFast",
                Method = "POST",
                Url = $"{(_payFastPaymentSettings.UseSandbox ? "https://sandbox.payfast.co.za" : "https://www.payfast.co.za")}/eng/process?"
            };
            post.Add("merchant_id", _payFastPaymentSettings.MerchantId);
            post.Add("merchant_key", _payFastPaymentSettings.MerchantKey);
            post.Add("return_url", $"{storeLocation}checkout/completed/{postProcessPaymentRequest.Order.Id}");
            post.Add("cancel_url", $"{storeLocation}orderdetails/{postProcessPaymentRequest.Order.Id}");
            post.Add("notify_url", $"{storeLocation}Plugins/PaymentPayFast/PaymentResult");
            post.Add("m_payment_id", postProcessPaymentRequest.Order.OrderGuid.ToString());
            post.Add("amount", postProcessPaymentRequest.Order.OrderTotal.ToString("0.00", CultureInfo.InvariantCulture));
            post.Add("item_name", $"Order #{postProcessPaymentRequest.Order.Id}");
            if (postProcessPaymentRequest.Order.BillingAddress != null)
            {
                post.Add("name_first", postProcessPaymentRequest.Order.BillingAddress.FirstName);
                post.Add("name_last", postProcessPaymentRequest.Order.BillingAddress.LastName);
                post.Add("email_address", postProcessPaymentRequest.Order.BillingAddress.Email);
            }

            post.Post();
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService, cart,
                _payFastPaymentSettings.AdditionalFee, _payFastPaymentSettings.AdditionalFeePercentage);
            return result;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring Payment method not supported");
            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            //always success
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring Payment method not supported");
            return result;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            return order.OrderStatus == OrderStatus.Pending;
        }

        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            return new List<string>();
        }
        
        public  ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentPayFast/Configure";
        }

        public void GetPublicViewComponent(out string viewComponentName)
        {
            viewComponentName = "PaymentPayFast";
        }

        /// <summary>
        /// Get type of the controller
        /// </summary>
        /// <returns>Controller type</returns>
        public Type GetControllerType()
        {
            return typeof(PaymentPayFastController);
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new PayFastPaymentSettings
            {
                UseSandbox = true
            });

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFast.Fields.AdditionalFee", "Additional fee");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFast.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFast.Fields.AdditionalFeePercentage", "Additional fee. Use percentage");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFast.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFast.Fields.MerchantId", "Merchant ID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFast.Fields.MerchantId.Hint", "Specify merchant ID.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFast.Fields.MerchantKey", "Merchant key");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFast.Fields.MerchantKey.Hint", "Specify merchant key.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFast.Fields.UseSandbox", "Use Sandbox");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFast.Fields.UseSandbox.Hint", "Check to enable Sandbox (testing environment).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFast.RedirectionTip", "You will be redirected to PayFast site to complete the order.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayFast.PaymentMethodDescription", "You will be redirected to PayFast site to complete the order.");

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<PayFastPaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.PayFast.Fields.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFast.Fields.AdditionalFee.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFast.Fields.AdditionalFeePercentage");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFast.Fields.AdditionalFeePercentage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFast.Fields.MerchantId");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFast.Fields.MerchantId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFast.Fields.MerchantKey");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFast.Fields.MerchantKey.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFast.Fields.UseSandbox");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFast.Fields.UseSandbox.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFast.RedirectionTip");
            this.DeletePluginLocaleResource("Plugins.Payments.PayFast.PaymentMethodDescription");

            base.Uninstall();
        }

        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Redirection; }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            get { return _localizationService.GetResource("Plugins.Payments.PayFast.PaymentMethodDescription"); }
        }

        #endregion
    }
}
