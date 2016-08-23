using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayFast.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Controllers;

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

        #endregion

        #region Ctor

        public PaymentPayFastController(ILocalizationService localizationService,
            ILogger logger,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            ISettingService settingService,
            PayFastPaymentSettings payFastPaymentSettings)
	    {
            this._localizationService = localizationService;
            this._logger = logger;
            this._orderProcessingService = orderProcessingService;
            this._orderService = orderService;
            this._settingService = settingService;
            this._payFastPaymentSettings = payFastPaymentSettings;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Validate Instant Transaction Notification Callback
        /// </summary>
        /// <param name="form">List of parameters</param>
        /// <param name="order">Order</param>
        /// <returns>true if there are no errors; otherwise false</returns>
        protected bool ValidateITN(FormCollection form, out Order order)
        {
            order = null;

            //validate order
            Guid orderGuid;
            if (!Guid.TryParse(form["m_payment_id"], out orderGuid))
                return false;

            order = _orderService.GetOrderByGuid(orderGuid);
            if (order == null)
            {
                _logger.Error(string.Format("PayFast ITN error: Order with guid {0} is not found", orderGuid));
                return false;
            }

            //validate merchant ID
            if (!form["merchant_id"].Equals(_payFastPaymentSettings.MerchantId, StringComparison.InvariantCulture))
            {
                _logger.Error("PayFast ITN error: Merchant ID mismatch");
                return false;
            }

            //validate IP address
            IPAddress ipAddress;
            if (!IPAddress.TryParse(Request.ServerVariables["REMOTE_ADDR"], out ipAddress))
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
            }.SelectMany(dns => Dns.GetHostAddresses(dns));
            if (!validIPs.Contains(ipAddress))
            {
                _logger.Error(string.Format("PayFast ITN error: IP address {0} is not valid", ipAddress));
                return false;
            }

            //validate data
            form.Remove("signature");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Add(form);
            var postData = Encoding.Default.GetBytes(parameters.ToString());
            var request = (HttpWebRequest)WebRequest.Create(string.Format("{0}/eng/query/validate",
                _payFastPaymentSettings.UseSandbox ? "https://sandbox.payfast.co.za" : "https://www.payfast.co.za"));
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
                    var responseParams = HttpUtility.ParseQueryString(streamReader.ReadToEnd());
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
            if (!form["payment_status"].Equals("COMPLETE", StringComparison.InvariantCulture))
            {
                _logger.Error(string.Format("PayFast ITN error: order #{0} is {1}", order.Id, form["payment_status"]));
                return false;
            }

            return true;
        }

        #endregion

        #region Methods

        [AdminAuthorize]
		[ChildActionOnly]
		public ActionResult Configure()
		{
            var model = new ConfigurationModel
            {
                MerchantId = _payFastPaymentSettings.MerchantId,
                MerchantKey = _payFastPaymentSettings.MerchantKey,
                UseSandbox = _payFastPaymentSettings.UseSandbox,
                AdditionalFee = _payFastPaymentSettings.AdditionalFee,
                AdditionalFeePercentage = _payFastPaymentSettings.AdditionalFeePercentage
            };

            return View("~/Plugins/Payments.PayFast/Views/PaymentPayFast/Configure.cshtml", model);
		}

		[HttpPost]
		[AdminAuthorize]
		[ChildActionOnly]
		public ActionResult Configure(ConfigurationModel model)
		{
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

		[ChildActionOnly]
		public ActionResult PaymentInfo()
		{
            return View("~/Plugins/Payments.PayFast/Views/PaymentPayFast/PaymentInfo.cshtml");
		}

		[NonAction]
		public override IList<string> ValidatePaymentForm(FormCollection form)
		{
			return new List<string>();
		}

		[NonAction]
		public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
		{
			return new ProcessPaymentRequest();
		}

        [ValidateInput(false)]
		public ActionResult PayFastResultHandler(FormCollection form)
		{
            //validation
            Order order;
            if (!ValidateITN(form, out order))
                return new HttpStatusCodeResult(HttpStatusCode.OK);

            //paid order
            if (_orderProcessingService.CanMarkOrderAsPaid(order))
            {
                order.AuthorizationTransactionId = form["pf_payment_id"];
                _orderService.UpdateOrder(order);
                _orderProcessingService.MarkOrderAsPaid(order);
            }

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        #endregion
    }
}