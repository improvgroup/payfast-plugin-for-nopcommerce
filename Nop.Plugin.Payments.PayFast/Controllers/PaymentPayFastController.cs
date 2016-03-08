using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.PayFast.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using System.Collections.Specialized;

namespace Nop.Plugin.Payments.PayFast.Controllers
{
	public class PaymentPayFastController : BasePaymentController
	{
		private readonly ISettingService _settingService;
		private readonly PayFastPaymentSettings _PayFastPaymentSettings;
		private readonly IOrderService _orderService;
		private readonly IOrderProcessingService _orderProcessingService;
		private readonly ILogger _logger;

	    public PaymentPayFastController(ISettingService settingService, PayFastPaymentSettings PayFastPaymentSettings,
	                                    IOrderService orderService,
	                                    IOrderProcessingService orderProcessingService,
	                                    ILogger logger)
	    {
	        this._settingService = settingService;
	        this._PayFastPaymentSettings = PayFastPaymentSettings;
	        this._orderService = orderService;
	        this._orderProcessingService = orderProcessingService;
	        this._logger = logger;
	    }

	    [AdminAuthorize]
		[ChildActionOnly]
		public ActionResult Configure()
		{
			var model = new ConfigurationModel();
			model.Url = _PayFastPaymentSettings.Url;
			model.merchant_id = _PayFastPaymentSettings.merchant_id;
			model.ValidateUrl = _PayFastPaymentSettings.ValidateUrl;
            model.merchant_key = _PayFastPaymentSettings.merchant_key;
            return View("~/Plugins/Payments.PayFast/Views/PaymentPayFast/Configure.cshtml", model);
		}

		[HttpPost]
		[AdminAuthorize]
		[ChildActionOnly]
		public ActionResult Configure(ConfigurationModel model)
		{
			//if (!ModelState.IsValid)
			//    return Configure();
			_PayFastPaymentSettings.Url = model.Url;
			_PayFastPaymentSettings.ValidateUrl = model.ValidateUrl;
			_PayFastPaymentSettings.merchant_id = model.merchant_id;
			_PayFastPaymentSettings.merchant_key = model.merchant_key;
			_settingService.SaveSetting(_PayFastPaymentSettings);

		    return Configure();
		}

		[ChildActionOnly]
		public ActionResult PaymentInfo()
		{
            var model = new PaymentInfoModel();
            return View("~/Plugins/Payments.PayFast/Views/PaymentPayFast/PaymentInfo.cshtml", model);
		}

		[NonAction]
		public override IList<string> ValidatePaymentForm(FormCollection form)
		{
			var warnings = new List<string>();
			return warnings;
		}

		[NonAction]
		public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
		{
			var paymentInfo = new ProcessPaymentRequest();
			return paymentInfo;
		}

		[ValidateInput(false)]
		public ActionResult PayFastResultHandler(FormCollection form)
		{
				string result = "";
				bool ValidResponse = true;

				NameValueCollection formVariables = Request.Form;
				result = formVariables["payment_status"];
				
				int OrderID = Convert.ToInt32(formVariables["m_payment_id"]);
				Order order = _orderService.GetOrderById(OrderID);

				NameValueCollection formVariablesWithoutHash = new NameValueCollection();
				foreach (var item in formVariables.AllKeys.Where(x => x.ToLower() != "signature"))
					formVariablesWithoutHash.Add(item, formVariables[item]);

				ValidResponse = PayFastPaymentProcessor.ValidateITNRequest(Request.ServerVariables["REMOTE_ADDR"], _logger);
				ValidResponse =  PayFastPaymentProcessor.ValidateITNRequestData(formVariablesWithoutHash,_PayFastPaymentSettings,_logger);
				
				if (ValidResponse)//todo set to ValidResponse
				{
                    //Success
					if ((result == "COMPLETE") && (formVariables["merchant_id"] == _PayFastPaymentSettings.merchant_id))
					{
						_logger.InsertLog(Core.Domain.Logging.LogLevel.Information, "Payment Result", "Success");
						_orderProcessingService.MarkOrderAsPaid(order);
						_orderService.UpdateOrder(order);
                        return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
					}
					//Failed
					else
					{
						_logger.InsertLog(Core.Domain.Logging.LogLevel.Information, "Payment Result", "Failed");
						var model = new PaymentResultModel();
						model.OrderID = OrderID.ToString();
                        model.Message = "Payment Failed";
                        return View("~/Plugins/Payments.PayFast/Views/PaymentPayFast/PaymentResultView.cshtml", model);

					}
				}
				else
				{
					var model = new PaymentResultModel();
					model.OrderID = OrderID.ToString();
					model.Message = "ValidateITNRequestData failed";

                    return View("~/Plugins/Payments.PayFast/Views/PaymentPayFast/PaymentResultView.cshtml", model);
				}
		}
	}
}