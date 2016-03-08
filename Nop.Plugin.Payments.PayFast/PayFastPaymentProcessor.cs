using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Routing;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.PayFast.Controllers;
using Nop.Services.Configuration;
using Nop.Services.Logging;
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

        private readonly PayFastPaymentSettings _PayFastPaymentSettings;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;


        #endregion

        #region Ctor

        public PayFastPaymentProcessor(PayFast.PayFastPaymentSettings PayFastPaymentSettings,
                                       ISettingService settingService, IWebHelper webHelper)
        {
            this._PayFastPaymentSettings = PayFastPaymentSettings;
            this._settingService = settingService;
            this._webHelper = webHelper;
        }

        #endregion

        #region Utilities


        /// <summary>
        /// Posts the data back to the payment processor to validate the data received
        /// </summary>
        public static bool ValidateITNRequestData(NameValueCollection formVariables,
                                                  PayFastPaymentSettings PayFastPaymentSettings, ILogger _logger)
        {
            bool isValid = true;
            try
            {
                StringBuilder sb = new StringBuilder();
                bool first = true;
                foreach (var item in formVariables)
                {
                    if (first) first = false;
                    else sb.Append("&");
                    sb.AppendFormat("{0}={1}", item.ToString(), HttpUtility.UrlEncode(formVariables[item.ToString()]));
                }
                byte[] postBytes = Encoding.ASCII.GetBytes(sb.ToString());
                _logger.InsertLog(Core.Domain.Logging.LogLevel.Information, "Post String", sb.ToString(), null);

                string validateUrl = PayFastPaymentSettings.ValidateUrl;
                HttpWebRequest req = HttpWebRequest.Create(validateUrl) as HttpWebRequest;
                req.Method = "POST";
                req.ContentType = "application/x-www-form-urlencoded";
                req.ContentLength = postBytes.Length;

                // add post data to request
                using (var postStream = req.GetRequestStream())
                {
                    postStream.Write(postBytes, 0, postBytes.Length);
                }

                string result = null;
                using (var responseStream = new StreamReader(req.GetResponse().GetResponseStream()))
                {
                    result = HttpUtility.UrlDecode(responseStream.ReadToEnd());
                    _logger.InsertLog(Core.Domain.Logging.LogLevel.Information, "Post response", result, null);
                }

                result = result.Replace("\r\n", " ").Replace("\r", "").Replace("\n", " ");
                if (result == null || !result.StartsWith("VALID", StringComparison.OrdinalIgnoreCase))
                {
                    isValid = false;

                }

            }
            catch (Exception ex)
            {
                _logger.InsertLog(Core.Domain.Logging.LogLevel.Error,
                                  "PayFast ITN Request is invalid", ex.Message.ToString());
                isValid = false;
            }

            return isValid;
        }

        public static bool ValidateITNRequest(String requestIp, ILogger _logger)
        {
            bool isValid = true;
            string[] validSites = new string[]
                {
                    "www.payfast.co.za",
                    "sandbox.payfast.co.za",
                    "w1w.payfast.co.za",
                    "w2w.payfast.co.za"
                };

            List<IPAddress> validIpAddresses = new List<IPAddress>();
            foreach (var url in validSites)
                validIpAddresses.AddRange(Dns.GetHostAddresses(url));

            _logger.InsertLog(Core.Domain.Logging.LogLevel.Information,
                              "IP of post source", requestIp);
            if (string.IsNullOrEmpty(requestIp))
            {
                _logger.InsertLog(Core.Domain.Logging.LogLevel.Error,
                                  "PayFast ITN Request is invalid", "The source IP address of the ITN request was null");
                isValid = false;
            }


            if (isValid)
                if (!validIpAddresses.Contains(IPAddress.Parse(requestIp)))
                {
                    _logger.InsertLog(Core.Domain.Logging.LogLevel.Error,
                                      "PayFast ITN Request is invalid",
                                      string.Format("The source IP address of the ITN request ({0}) is not valid",
                                                    requestIp));
                    isValid = false;
                }

            return isValid;
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
            var result = new ProcessPaymentResult();

            result.NewPaymentStatus = PaymentStatus.Pending;

            return result;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var post = new RemotePost();
            var orderId = postProcessPaymentRequest.Order.Id.ToString();
            var orderTotal = postProcessPaymentRequest.Order.OrderTotal.ToString("0.00", CultureInfo.InvariantCulture);
            var storeLocation = _webHelper.GetStoreLocation(false);

            post.FormName = "PayFast";
            post.Url = _PayFastPaymentSettings.Url;
            post.Method = "POST";
            post.Add("merchant_id", _PayFastPaymentSettings.merchant_id);
            post.Add("merchant_key", _PayFastPaymentSettings.merchant_key);
            post.Add("return_url", storeLocation + "orderdetails/" + orderId);
            post.Add("cancel_url", storeLocation + "orderdetails/" + orderId);
            post.Add("notify_url", storeLocation + "Plugins/PaymentPayFast/PaymentResult");
            post.Add("m_payment_id", orderId);
            post.Add("amount", orderTotal);
            post.Add("item_name", "Online Purchase, Order number: " + orderId);
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
            return 0;
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

            if (order.OrderStatus == OrderStatus.Pending)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName,
                                          out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentPayFast";
            routeValues = new RouteValueDictionary()
                {
                    {"Namespaces", "Nop.Plugin.Payments.PayFast.Controllers"},
                    {"area", null}
                };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName,
                                        out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentPayFast";
            routeValues = new RouteValueDictionary()
                {
                    {"Namespaces", "Nop.Plugin.Payments.PayFast.Controllers"},
                    {"area", null}
                };
        }

        public Type GetControllerType()
        {
            return typeof (PaymentPayFastController);
        }

        public override void Install()
        {
            var settings = new PayFastPaymentSettings()
                {
                    Url = "https://sandbox.payfast.co.za/eng/process",
                    ValidateUrl = "https://sandbox.payfast.co.za/eng/query/validate",
                    merchant_id = "10000103",
                    merchant_key = "479f49451e829"

                };
            _settingService.SaveSetting(settings);

            base.Install();
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
            get { return RecurringPaymentType.Manual; }
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

        #endregion
    }
}
