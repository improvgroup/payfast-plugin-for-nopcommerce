using System;
using System.Linq;
using System.Net;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayFast.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.PayFast.Controllers
{
    public class PaymentPayFastController : BasePaymentController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly ISettingService _settingService;
        private readonly PayFastPaymentSettings _payFastPaymentSettings;
        private readonly IWebHelper _webHelper;
        private readonly IPermissionService _permissionService;

        #endregion

        #region Ctor

        public PaymentPayFastController(ILocalizationService localizationService,
            ILogger logger,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            ISettingService settingService,
            PayFastPaymentSettings payFastPaymentSettings,
            IWebHelper webHelper,
            IPermissionService permissionService)
        {
            this._localizationService = localizationService;
            this._logger = logger;
            this._orderProcessingService = orderProcessingService;
            this._orderService = orderService;
            this._settingService = settingService;
            this._payFastPaymentSettings = payFastPaymentSettings;
            this._webHelper = webHelper;
            this._permissionService = permissionService;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Validate Instant Transaction Notification Callback
        /// </summary>
        /// <param name="form">List of parameters</param>
        /// <param name="order">Order</param>
        /// <returns>true if there are no errors; otherwise false</returns>
        protected bool ValidateITN(IFormCollection form, out Order order)
        {
            order = null;

            //validate order
            if (!Guid.TryParse(form["m_payment_id"], out Guid orderGuid))
                return false;

            order = _orderService.GetOrderByGuid(orderGuid);
            if (order == null)
            {
                _logger.Error($"PayFast ITN error: Order with guid {orderGuid} is not found");
                return false;
            }

            //validate merchant ID
            if (!form["merchant_id"].ToString().Equals(_payFastPaymentSettings.MerchantId, StringComparison.InvariantCulture))
            {
                _logger.Error("PayFast ITN error: Merchant ID mismatch");
                return false;
            }

            //validate IP address
            if (!IPAddress.TryParse(_webHelper.GetCurrentIpAddress(), out IPAddress ipAddress))
            {
                _logger.Error("PayFast ITN error: IP address is empty");
                return false;
            }

            var validIPs = new[]
            {
                "www.payfast.co.za",
                "sandbox.payfast.co.za",
                "w1w.payfast.co.za",
                "w2w.payfast.co.za"
            }.SelectMany(Dns.GetHostAddresses);

            if (!validIPs.Contains(ipAddress))
            {
                _logger.Error($"PayFast ITN error: IP address {ipAddress} is not valid");
                return false;
            }

            //validate data
            var parameters = QueryHelpers.ParseQuery(string.Empty);
            foreach (var pair in form)
            {
                if (pair.Key.Equals("signature", StringComparison.InvariantCultureIgnoreCase))
                    parameters.Add(pair.Key, pair.Value);
            }
            var postData = Encoding.Default.GetBytes(parameters.ToString());
            var request = (HttpWebRequest)WebRequest.Create($"{(_payFastPaymentSettings.UseSandbox ? "https://sandbox.payfast.co.za" : "https://www.payfast.co.za")}/eng/query/validate");
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = postData.Length;

            try
            {
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(postData, 0, postData.Length);
                }
                var httpResponse = (HttpWebResponse)request.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var responseParams = QueryHelpers.ParseQuery(streamReader.ReadToEnd());
                    if (!responseParams.ToString().StartsWith("VALID", StringComparison.InvariantCulture))
                    {
                        _logger.Error("PayFast ITN error: passed data is not valid");
                        return false;
                    }
                }
            }
            catch (WebException)
            {
                _logger.Error("PayFast ITN error: passed data is not valid");
                return false;
            }

            //validate payment status
            if (!form["payment_status"].ToString().Equals("COMPLETE", StringComparison.InvariantCulture))
            {
                _logger.Error($"PayFast ITN error: order #{order.Id} is {form["payment_status"]}");
                return false;
            }

            return true;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            var model = new ConfigurationModel
            {
                MerchantId = _payFastPaymentSettings.MerchantId,
                MerchantKey = _payFastPaymentSettings.MerchantKey,
                UseSandbox = _payFastPaymentSettings.UseSandbox,
                AdditionalFee = _payFastPaymentSettings.AdditionalFee,
                AdditionalFeePercentage = _payFastPaymentSettings.AdditionalFeePercentage
            };

            return View("~/Plugins/Payments.PayFast/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            _payFastPaymentSettings.MerchantId = model.MerchantId;
            _payFastPaymentSettings.MerchantKey = model.MerchantKey;
            _payFastPaymentSettings.UseSandbox = model.UseSandbox;
            _payFastPaymentSettings.AdditionalFee = model.AdditionalFee;
            _payFastPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;

            _settingService.SaveSetting(_payFastPaymentSettings);

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        public IActionResult PayFastResultHandler(IpnModel model)
        {
            var form = model.Form;

            //validation
            if (!ValidateITN(form, out Order order))
                return new StatusCodeResult((int)HttpStatusCode.OK);

            //paid order
            if (_orderProcessingService.CanMarkOrderAsPaid(order))
            {
                order.AuthorizationTransactionId = form["pf_payment_id"];
                _orderService.UpdateOrder(order);
                _orderProcessingService.MarkOrderAsPaid(order);
            }

            return new StatusCodeResult((int)HttpStatusCode.OK);
        }

        #endregion
    }
}