using Microsoft.AspNetCore.Mvc;
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
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

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
        #region Methods       
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
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
        [AuthorizeAdmin]
        [AdminAntiForgery]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
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



        public IActionResult CancelOrder()
        {
            var order = _orderService.SearchOrders(storeId: _storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1).FirstOrDefault();
            if (order != null)
                return RedirectToRoute("OrderDetails", new { orderId = order.Id });

            return RedirectToRoute("HomePage");
        }

        public IActionResult ConfirmOrder()
        {
            var order = _orderService.SearchOrders(storeId: _storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1).FirstOrDefault();
            if (order != null)
                return RedirectToRoute("OrderDetails", new { orderId = order.Id });

            return RedirectToRoute("HomePage");
        }


        //public IActionResult CreatePayment(string AccessToken, string PayerId, string PaymentId, string PayPalMode)
        //{            
        //    string strApiContext = _workContext.CurrentCustomer.GetAttribute<string>("ApiContext", _storeContext.CurrentStore.Id);
        //    var accessToken =  Newtonsoft.Json.JsonConvert.DeserializeObject<AuthToken>(strApiContext);
        //    string host = _payPalPlusPaymentSettings.EnviromentSandBox;
        //    if (_payPalPlusPaymentSettings.UseSandbox == false)
        //        host = _payPalPlusPaymentSettings.EnviromentLive;

        //    Task.Run(async () =>
        //    {
        //        PayPalPaymentCreatedResponse createdPayment = await CreatePaypalPaymentAsync(host, accessToken, _workContext.CurrentCustomer);

        //        var approval_url = createdPayment.links.First(x => x.rel == "approval_url").href;
        //        var result = new { url=createdPayment.links[0].href};
        //        return result;

        //    }).Wait();

        //    return View("~/Plugins/Payments.PayPalPlus/Views/CreatePayment.cshtml");
        //}

        //public IActionResult BridgeToPay(string PayerId, string payerTokenCards, string step)
        //{
        //    //Redirect to IframePage
        //    RemotePost remotePost = new RemotePost();
        //    remotePost.FormName = "paymentppp";
        //    remotePost.Url = _webHelper.GetStoreLocation() + "/checkout/paymentinfo";
        //    remotePost.Add("payerId", PayerId);
        //    remotePost.Add("payerTokenCards", payerTokenCards);
        //    remotePost.Add("nextstep", step);
        //    remotePost.Post();
        //    return Content("");
        //}

        public IActionResult AprovalOrder()
        {

            return View("~/Plugins/Payments.PayPalPlus/Views/AprovalOrder.cshtml");
        }

        public ActionResult IPNHandler()
        {
            byte[] param;
            using (var stream = new MemoryStream())
            {
                this.Request.Body.CopyTo(stream);
                param = stream.ToArray();
            }
            string ipnData = Encoding.ASCII.GetString(param);

            _payPalIPNService.HandleIPN(ipnData);

            //nothing should be rendered to visitor
            return Content("");
        }

        #endregion
        //#region Tools
        //private async Task<PayPalPaymentCreatedResponse> CreatePaypalPaymentAsync(string host, AuthToken accessToken, Customer customer)
        //{
        //    System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        //    HttpClient http = new HttpClient
        //    {
        //        BaseAddress = new Uri(host),
        //        Timeout = TimeSpan.FromSeconds(30),
        //    };

        //    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "v1/payments/payment");
        //    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Access_Token);

        //    var model = new CreatePaymentModel();
        //    decimal? shoppingCartTotalBase = _orderTotalCalculationService.GetShoppingCartTotal(customer.ShoppingCartItems.ToList(), false);
        //    // shoppingCartTotalBase.Value
        //    var taxTotal = _orderTotalCalculationService.GetTaxTotal(customer.ShoppingCartItems.ToList(), true);
        //    var shipping = _orderTotalCalculationService.GetShoppingCartShippingTotal(customer.ShoppingCartItems.ToList(), false);
        //    var shippingTotal = shipping ?? 0;
        //    List<RestObjects.Item> items = new List<RestObjects.Item>();
        //    foreach (var ic in customer.ShoppingCartItems)
        //    {
        //        var item = new RestObjects.Item
        //        {
        //            currency = "MXN",
        //            name = ic.Product.Name,
        //            description = ic.Product.Name,
        //            quantity = ic.Quantity.ToString(),
        //            price = Math.Round(ic.Product.Price,2).ToString(),
        //            sku = ic.Product.Sku,

        //        };
        //        items.Add(item);
        //    }
        //    List<RestObjects.Transactions> transactions = new List<RestObjects.Transactions>();
        //    RestObjects.Transactions transaction = new RestObjects.Transactions()
        //    {
        //        amount = new RestObjects.AmountInfo
        //        {
        //            currency = "MXN",
        //            details = new DetailsAmountInfo
        //            {
        //                subtotal = Math.Round(shoppingCartTotalBase.Value, 2).ToString()
        //            },
        //            total = Math.Round(shoppingCartTotalBase.Value, 2).ToString(),
        //        },

        //        description = "This is the payment transaction description",
        //        custom = "This is a custom field you can use to identify orders for example",
        //        payment_options = new RestObjects.PaymentOptions(),
        //        item_list = new RestObjects.Items()
        //        {
        //            items = items,
        //            shipping_address = new RestObjects.ShippingAddressInfo()
        //            {
        //                city = "Mexico",
        //                country_code = "MX",
        //                postal_code = "06700",
        //                phone = "555555555",
        //                state = "CDMX",
        //                recipient_name = "direccion",
        //                line1 = "linea1",
        //                line2 = "linea2",
        //            }
        //        },
        //    };
        //    transactions.Add(transaction);

        //    RestObjects.Payment paymentRest = new RestObjects.Payment
        //    {
        //        intent = "sale",
        //        application_context = new RestObjects.AplicationContext(),
        //        payer = new RestObjects.Payer(),
        //        transactions = transactions,
        //        redirect_urls = new RestObjects.RedirectUrlsInfo
        //        {
        //            return_url = $"{_webHelper.GetStoreLocation()}Plugins/PaymentPayPalPlus/AprovalOrder",
        //            cancel_url = $"{_webHelper.GetStoreLocation()}Plugins/PaymentPayPalPlus/CancelOrder",
        //        },
        //    };
        //    var data = Newtonsoft.Json.JsonConvert.SerializeObject(paymentRest);


        //    request.Content = new StringContent(data, Encoding.UTF8, "application/json");

        //    HttpResponseMessage response = await http.SendAsync(request);

        //    string content = await response.Content.ReadAsStringAsync();
        //    PayPalPaymentCreatedResponse paypalPaymentCreated = JsonConvert.DeserializeObject<PayPalPaymentCreatedResponse>(content);
        //    return paypalPaymentCreated;
        //}
        //#endregion

    }
}
