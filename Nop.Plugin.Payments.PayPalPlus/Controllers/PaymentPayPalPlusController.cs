using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.PayPalPlus.Models;
using Nop.Plugin.Payments.PayPalPlus.Services;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Nop.Plugin.Payments.PayPalPlus.Controllers
{
    public class PaymentPayPalPlusController : BasePaymentController
    {
        #region Fields
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IPermissionService _permissionService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly IStoreContext _storeContext;
        private readonly ILogger _logger;
        private readonly IWebHelper _webHelper;
        private readonly PaymentSettings _paymentSettings;
        private readonly PayPalPlusPaymentSettings _payPalPlusPaymentSettings;
        private readonly ShoppingCartSettings _shoppingCartSettings;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IPayPalIPNService _payPalIPNService;
        #endregion
        #region Ctor
        public PaymentPayPalPlusController(IWorkContext workContext,
            IStoreService storeService,
            ISettingService settingService,
            IPaymentService paymentService,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            IPermissionService permissionService,
            IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            IStoreContext storeContext,
            ILogger logger,
            IWebHelper webHelper,
            PaymentSettings paymentSettings,
            PayPalPlusPaymentSettings payPalPlusPaymentSettings,
            ShoppingCartSettings shoppingCartSettings,
            IOrderTotalCalculationService orderTotalCalculationService,
            IPayPalIPNService payPalIPNService)
        {
            this._workContext = workContext;
            this._storeService = storeService;
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._permissionService = permissionService;
            this._genericAttributeService = genericAttributeService;
            this._localizationService = localizationService;
            this._storeContext = storeContext;
            this._logger = logger;
            this._webHelper = webHelper;
            this._paymentSettings = paymentSettings;
            this._payPalPlusPaymentSettings = payPalPlusPaymentSettings;
            this._shoppingCartSettings = shoppingCartSettings;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._payPalIPNService = payPalIPNService;
        }
        #endregion

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {           
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var payPalPlusPaymentSettings = _settingService.LoadSetting<PayPalPlusPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                UseSandbox = payPalPlusPaymentSettings.UseSandbox,
                Name = payPalPlusPaymentSettings.Name,
                ClientId = payPalPlusPaymentSettings.ClientId,
                SecretId = payPalPlusPaymentSettings.SecretId,
                IFrameHeight = payPalPlusPaymentSettings.IFrameHeight,
                Language = payPalPlusPaymentSettings.Language,
                EnviromentLive = payPalPlusPaymentSettings.EnviromentLive,
                EnviromentSandBox = payPalPlusPaymentSettings.EnviromentSandBox,
                AdditionalFee = payPalPlusPaymentSettings.AdditionalFee,
                AdditionalFeePercentage = payPalPlusPaymentSettings.AdditionalFeePercentage,
                ActiveStoreScopeConfiguration = storeScope,
                DisallowRememberedCards = payPalPlusPaymentSettings.DisallowRememberedCards,
                Currency = payPalPlusPaymentSettings.Currency,
                CountryTwoLetters = payPalPlusPaymentSettings.CountryTwoLetters,
                ScriptCheckOutOnePage = _localizationService.GetResource("Plugins.Payments.PayPalPlus.Fields.scriptOne"),
                ScriptCheckOutPageToPage = _localizationService.GetResource("Plugins.Payments.PayPalPlus.Fields.scriptPage"),
            };
            if (storeScope > 0)
            {
                model.UseSandbox_OverrideForStore = _settingService.SettingExists(payPalPlusPaymentSettings, x => x.UseSandbox, storeScope);
                model.Name_OverrideForStore = _settingService.SettingExists(payPalPlusPaymentSettings, x => x.Name, storeScope);
                model.ClientId_OverrideForStore = _settingService.SettingExists(payPalPlusPaymentSettings, x => x.ClientId, storeScope);
                model.SecretId_OverrideForStore = _settingService.SettingExists(payPalPlusPaymentSettings, x => x.SecretId, storeScope);
                model.IFrameHeight_OverrideForStore = _settingService.SettingExists(payPalPlusPaymentSettings, x => x.IFrameHeight, storeScope);
                model.Language_OverrideForStore = _settingService.SettingExists(payPalPlusPaymentSettings, x => x.Language, storeScope);
                model.EnviromentSandBox_OverrideForStore = _settingService.SettingExists(payPalPlusPaymentSettings, x => x.EnviromentSandBox, storeScope);
                model.EnviromentLive_OverrideForStore = _settingService.SettingExists(payPalPlusPaymentSettings, x => x.EnviromentLive, storeScope);
                model.AdditionalFee_OverrideForStore = _settingService.SettingExists(payPalPlusPaymentSettings, x => x.AdditionalFee, storeScope);
                model.AdditionalFeePercentage_OverrideForStore = _settingService.SettingExists(payPalPlusPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
                model.DisallowRememberedCards_OverrideForStore = _settingService.SettingExists(payPalPlusPaymentSettings, x => x.DisallowRememberedCards, storeScope);
                model.Currency_OverrideForStore = _settingService.SettingExists(payPalPlusPaymentSettings, x => x.Currency, storeScope);
                model.CountryTwoLetters_OverrideForStore = _settingService.SettingExists(payPalPlusPaymentSettings, x => x.CountryTwoLetters, storeScope);
            }

            return View("~/Plugins/Payments.PayPalPlus/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {            
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var payPalPlusPaymentSettings = _settingService.LoadSetting<PayPalPlusPaymentSettings>(storeScope);

            //save settings
            payPalPlusPaymentSettings.UseSandbox = model.UseSandbox;
            payPalPlusPaymentSettings.Name = model.Name;
            payPalPlusPaymentSettings.ClientId = model.ClientId;
            payPalPlusPaymentSettings.SecretId = model.SecretId;
            payPalPlusPaymentSettings.IFrameHeight = model.IFrameHeight;
            payPalPlusPaymentSettings.Language = model.Language;
            payPalPlusPaymentSettings.EnviromentLive = model.EnviromentLive;
            payPalPlusPaymentSettings.EnviromentSandBox = model.EnviromentSandBox;
            payPalPlusPaymentSettings.DisallowRememberedCards = model.DisallowRememberedCards;
            payPalPlusPaymentSettings.AdditionalFee = model.AdditionalFee;
            payPalPlusPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
            payPalPlusPaymentSettings.Currency = model.Currency;
            payPalPlusPaymentSettings.CountryTwoLetters = model.CountryTwoLetters;
            var scriptOne = _localizationService.GetLocaleStringResourceByName("Plugins.Payments.PayPalPlus.Fields.scriptOne");
            scriptOne.ResourceValue = model.ScriptCheckOutOnePage;
            _localizationService.UpdateLocaleStringResource(scriptOne);

            var scriptPage = _localizationService.GetLocaleStringResourceByName("Plugins.Payments.PayPalPlus.Fields.scriptPage");
            scriptPage.ResourceValue = model.ScriptCheckOutPageToPage;
            _localizationService.UpdateLocaleStringResource(scriptPage);

            //payPalPlusPaymentSettings.ScriptCheckOutOnePage = model.ScriptCheckOutOnePage;
            //payPalPlusPaymentSettings.ScriptCheckOutPageToPage = model.ScriptCheckOutPageToPage;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            _settingService.SaveSettingOverridablePerStore(payPalPlusPaymentSettings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payPalPlusPaymentSettings, x => x.Name, model.Name_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payPalPlusPaymentSettings, x => x.ClientId, model.ClientId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payPalPlusPaymentSettings, x => x.SecretId, model.SecretId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payPalPlusPaymentSettings, x => x.Language, model.Language_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payPalPlusPaymentSettings, x => x.IFrameHeight, model.IFrameHeight_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payPalPlusPaymentSettings, x => x.EnviromentLive, model.EnviromentLive_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payPalPlusPaymentSettings, x => x.EnviromentSandBox, model.EnviromentSandBox_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payPalPlusPaymentSettings, x => x.DisallowRememberedCards, model.DisallowRememberedCards_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payPalPlusPaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payPalPlusPaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payPalPlusPaymentSettings, x => x.Currency, model.Currency_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payPalPlusPaymentSettings, x => x.CountryTwoLetters, model.CountryTwoLetters_OverrideForStore, storeScope, false);
            //_settingService.SaveSettingOverridablePerStore(payPalPlusPaymentSettings, x => x.ScriptCheckOutPageToPage, model.ScriptCheckOutPageToPage_OverrideForStore, storeScope, false);
            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [ValidateInput(false)]
        public ActionResult IPNHandler()
        {
            byte[] param = Request.BinaryRead(Request.ContentLength);
            string ipnData = Encoding.ASCII.GetString(param);

            _payPalIPNService.HandleIPN(ipnData);

            //nothing should be rendered to visitor
            return Content("");
        }

        //public ActionResult IPNHandler()
        //{
        //    byte[] param;
        //    using (var stream = new MemoryStream())
        //    {
        //        this.Request.Body.CopyTo(stream);
        //        param = stream.ToArray();
        //    }
        //    string ipnData = Encoding.ASCII.GetString(param);

        //    _payPalIPNService.HandleIPN(ipnData);

        //    //nothing should be rendered to visitor
        //    return Content("");
        //}

        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var result = new List<string>();
            string payerId = form["payerId"];
            
            if (string.IsNullOrEmpty(payerId) || string.IsNullOrWhiteSpace(payerId))
                result.Add("PayerId no enviado");

            return result;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {        
            ProcessPaymentRequest paymentRequest = new ProcessPaymentRequest();
            string payerId = form["payerId"];
            string payerTokenCards = form["payerTokenCards"];
            if (!string.IsNullOrEmpty(payerId))
                paymentRequest.CustomValues.Add("PaypalPayerId", payerId.ToString());

            if (!string.IsNullOrEmpty(payerTokenCards))
            {                
                    _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer, "PPPTokenCards", payerTokenCards, _storeContext.CurrentStore.Id);                
            }

            return paymentRequest;
        }

        public ActionResult CancelOrder()
        {
            var order = _orderService.SearchOrders(storeId: _storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1).FirstOrDefault();
            if (order != null)
                return RedirectToRoute("OrderDetails", new { orderId = order.Id });

            return RedirectToRoute("HomePage");
        }

        public ActionResult AprovalOrder()
        {

            return View("~/Plugins/Payments.PayPalPlus/Views/AprovalOrder.cshtml");
        }
       
    }
}
