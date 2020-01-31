using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Plugin.Payments.PayPalPlus.Models;
using Nop.Plugin.Payments.PayPalPlus.RestObjects;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Shipping;
using Nop.Services.Stores;
using Nop.Services.Tax;
using Nop.Web.Framework.Components;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.PayPalPlus.Components
{
    [ViewComponent(Name = "PaymentPayPalPlus")]
    public class PaymentPayPalPlusViewComponent : NopViewComponent
    {

        #region Fields
        private readonly ILocalizationService _localizationService;
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IWebHelper _webHelper;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ShippingSettings _shippingSettings;
        private readonly OrderSettings _orderSettings;
        private readonly ILogger _logger;
        private readonly IProductAttributeParser _productAttributeParser;
        private readonly IPriceCalculationService _priceCalculationService;
        private readonly ITaxService _taxService;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly IPaymentService _paymentService;
        private readonly PaymentSettings _paymentSettings;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly IShippingPluginManager _shippingPluginManager;
        #endregion

        #region Ctor
        public PaymentPayPalPlusViewComponent(ILocalizationService localizationService,
            IWorkContext workContext, IStoreService storeService, ISettingService settingService, IStoreContext storeContext,
            IGenericAttributeService genericAttributeService, IWebHelper webHelper,
            IOrderTotalCalculationService orderTotalCalculationService, IHttpContextAccessor httpContextAccessor,
            ShippingSettings shippingSettings, OrderSettings orderSettings, ILogger logger,
            IProductAttributeParser productAttributeParser,
            IPriceCalculationService priceCalculationService,
            ITaxService taxService,
            ICheckoutAttributeParser checkoutAttributeParser,
            IPaymentService paymentService,
            IPaymentPluginManager paymentPluginManager,
            IShippingPluginManager shippingPluginManager,
            PaymentSettings paymentSettings)
        {
            this._localizationService = localizationService;
            this._workContext = workContext;
            this._storeService = storeService;
            this._settingService = settingService;
            this._storeContext = storeContext;
            this._genericAttributeService = genericAttributeService;
            this._webHelper = webHelper;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._httpContextAccessor = httpContextAccessor;
            this._shippingSettings = shippingSettings;
            this._orderSettings = orderSettings;
            this._logger = logger;
            this._productAttributeParser = productAttributeParser;
            this._priceCalculationService = priceCalculationService;
            this._taxService = taxService;
            this._checkoutAttributeParser = checkoutAttributeParser;
            this._paymentService = paymentService;
            this._paymentPluginManager = paymentPluginManager;
            this._shippingPluginManager = shippingPluginManager;
            this._paymentSettings = paymentSettings;
        }
        #endregion

        #region Utilities


        #region Items

        /// <summary>
        /// Get PayPal items
        /// </summary>
        /// <param name="shoppingCart">Shopping cart</param>
        /// <param name="customer">Customer</param>
        /// <param name="storeId">Store identifier</param>
        /// <param name="currencyCode">Currency code</param>
        /// <returns>List of PayPal items</returns>
        protected List<Item> GetItems(IList<ShoppingCartItem> shoppingCart, Customer customer, int storeId, string currencyCode)
        {
            var items = new List<Item>();

            //create PayPal items from shopping cart items
            items.AddRange(CreateItems(shoppingCart));

            //create PayPal items from checkout attributes
            items.AddRange(CreateItemsForCheckoutAttributes(customer, storeId));

            //create PayPal item for payment method additional fee
            items.Add(CreateItemForPaymentAdditionalFee(shoppingCart, customer));

            //currently there are no ways to add discount for all order directly to amount details, so we add them as extra items 
            //create PayPal item for subtotal discount
            items.Add(CreateItemForSubtotalDiscount(shoppingCart));

            //create PayPal item for total discount
            items.Add(CreateItemForTotalDiscount(shoppingCart));

            items.RemoveAll(item => item == null);

            //add currency code for all items
            items.ForEach(item => item.currency = currencyCode);

            return items;
        }

        /// <summary>
        /// Create items from shopping cart
        /// </summary>
        /// <param name="shoppingCart">Shopping cart</param>
        /// <returns>Collection of PayPal items</returns>
        protected IEnumerable<Item> CreateItems(IEnumerable<ShoppingCartItem> shoppingCart)
        {
            return shoppingCart.Select(shoppingCartItem =>
            {
                if (shoppingCartItem.Product == null)
                    return null;

                var item = new Item
                {

                    //name
                    name = shoppingCartItem.Product.Name
                };

                //SKU
                if (!string.IsNullOrEmpty(shoppingCartItem.AttributesXml))
                {
                    var combination = _productAttributeParser.FindProductAttributeCombination(shoppingCartItem.Product, shoppingCartItem.AttributesXml);
                    item.sku = combination != null && !string.IsNullOrEmpty(combination.Sku) ? combination.Sku : shoppingCartItem.Product.Sku;
                }
                else
                    item.sku = shoppingCartItem.Product.Sku;

                //item price
                var unitPrice = _priceCalculationService.GetUnitPrice(shoppingCartItem);
                var price = _taxService.GetProductPrice(shoppingCartItem.Product, unitPrice, false, shoppingCartItem.Customer, out decimal _);
                item.price = price.ToString("N", new CultureInfo("en-US"));

                //quantity
                item.quantity = shoppingCartItem.Quantity.ToString();

                return item;
            });
        }

        /// <summary>
        /// Create items for checkout attributes
        /// </summary>
        /// <param name="customer">Customer</param>
        /// <param name="storeId">Store identifier</param>
        /// <returns>Collection of PayPal items</returns>
        protected IEnumerable<Item> CreateItemsForCheckoutAttributes(Customer customer, int storeId)
        {
            var checkoutAttributesXml = _genericAttributeService.GetAttribute<string>(customer,NopCustomerDefaults.CheckoutAttributes);
            if (string.IsNullOrEmpty(checkoutAttributesXml))
                return new List<Item>();

            //get attribute values
            var attributeValues = _checkoutAttributeParser.ParseCheckoutAttributeValues(checkoutAttributesXml);

            return attributeValues.Select(checkoutAttributeValue =>
            {
                if (checkoutAttributeValue.CheckoutAttribute == null)
                    return null;

                //get price
                var attributePrice = _taxService.GetCheckoutAttributePrice(checkoutAttributeValue, false, customer);

                //create item
                return new Item
                {
                    name = $"{checkoutAttributeValue.CheckoutAttribute.Name} ({checkoutAttributeValue.Name})",
                    price = attributePrice.ToString("N", new CultureInfo("en-US")),
                    quantity = "1"
                };
            });
        }

        /// <summary>
        /// Create item for payment method additional fee
        /// </summary>
        /// <param name="shoppingCart">Shopping cart</param>
        /// <param name="customer">Customer</param>
        /// <returns>PayPal item</returns>
        protected Item CreateItemForPaymentAdditionalFee(IList<ShoppingCartItem> shoppingCart, Customer customer)
        {
            //var processor = _paymentPluginManager.LoadPluginBySystemName("Payment.PayPalPlus") as PayPalPlusPaymentProcessor; //_paymentService.LoadPaymentMethodBySystemName("Payments.PayPalPlus") as PayPalPlusPaymentProcessor;
            //if (processor == null || !processor.PluginDescriptor.Installed)
            //    throw new NopException("PayPal Standard module cannot be loaded");
            //get price
            //var paymentAdditionalFee = _paymentService.GetAdditionalHandlingFee(shoppingCart, processor.PluginDescriptor.SystemName);
            var paymentAdditionalFee = _paymentService.GetAdditionalHandlingFee(shoppingCart, "Payment.PayPalPlus");
            var paymentPrice = _taxService.GetPaymentMethodAdditionalFee(paymentAdditionalFee, false, customer);

            if (paymentPrice <= decimal.Zero)
                return null;

            //create item
            return new Item
            {
                //name = $"Metodo de Pago (" + processor.PluginDescriptor.FriendlyName + ") Costo adicional",
                name = $"Metodo de Pago Costo adicional",
                price = paymentPrice.ToString("N", new CultureInfo("en-US")),
                quantity = "1"
            };
        }

        /// <summary>
        /// Create item for discount to order subtotal
        /// </summary>
        /// <param name="shoppingCart">Shopping cart</param>
        /// <returns>PayPal item</returns>
        protected Item CreateItemForSubtotalDiscount(IList<ShoppingCartItem> shoppingCart)
        {
            //get subtotal discount amount
            _orderTotalCalculationService.GetShoppingCartSubTotal(shoppingCart, false, out decimal discountAmount, out List<DiscountForCaching> _, out decimal _, out decimal _);

            if (discountAmount <= decimal.Zero)
                return null;

            //create item with negative price
            return new Item
            {
                name = "Discount for the subtotal of order",
                price = (-discountAmount).ToString("N", new CultureInfo("en-US")),
                quantity = "1"
            };
        }

        /// <summary>
        /// Create item for discount to order total 
        /// </summary>
        /// <param name="shoppingCart">Shopping cart</param>
        /// <returns>PayPal item</returns>
        protected Item CreateItemForTotalDiscount(IList<ShoppingCartItem> shoppingCart)
        {
            //get total discount amount
            var orderTotal = _orderTotalCalculationService.GetShoppingCartTotal(shoppingCart,
                out decimal discountAmount,
                out List<DiscountForCaching> _, out List<AppliedGiftCard> _, out int _, out decimal _);

            if (discountAmount <= decimal.Zero)
                return null;

            //create item with negative price
            return new Item
            {
                name = "Discount for the total of order",
                price = (-discountAmount).ToString("N", new CultureInfo("en-US")),
                quantity = "1"
            };
        }

        #endregion

        /// <summary>
        /// Get transaction amount details
        /// </summary>
        /// <param name="paymentRequest">Payment info required for an order processing</param>
        /// <param name="shoppingCart">Shopping cart</param>
        /// <param name="items">List of PayPal items</param>
        /// <returns>Amount details object</returns>
        protected DetailsAmountInfo GetAmountDetails(IList<ShoppingCartItem> shoppingCart, IList<Item> items, out decimal totalAdjust)
        {
            //get total discount amount
            var orderTotal = _orderTotalCalculationService.GetShoppingCartTotal(shoppingCart,
                out decimal discountAmount,
                out List<DiscountForCaching> _, out List<AppliedGiftCard> _, out int _, out decimal _);

            //get shipping total
            var shippingRateComputationMethods = _shippingPluginManager
                        .LoadActivePlugins(_workContext.CurrentCustomer, _storeContext.CurrentStore.Id);
            var shipping = _orderTotalCalculationService.GetShoppingCartShippingTotal(shoppingCart, shippingRateComputationMethods);
            var shippingTotal = shipping ?? 0;            
            
            //get tax total
            var taxTotal = _orderTotalCalculationService.GetTaxTotal(shoppingCart, shippingRateComputationMethods);

            //get subtotal
            decimal subTotal;
            if (items != null && items.Any())
            {
                //items passed to PayPal, so calculate subtotal based on them
                subTotal = items.Sum(item => !decimal.TryParse(item.price, out decimal tmpPrice) || !int.TryParse(item.quantity, out int tmpQuantity) ? 0 : tmpPrice * tmpQuantity);
            }
            else
                subTotal = orderTotal.Value - shippingTotal - taxTotal;

            //adjust order total to avoid PayPal payment error: "Transaction amount details (subtotal, tax, shipping) must add up to specified amount total"
            totalAdjust = Math.Round(shippingTotal, 2) + Math.Round(subTotal, 2) + Math.Round(taxTotal, 2);

            //create amount details
            return new DetailsAmountInfo
            {
                shipping = shippingTotal.ToString("N", new CultureInfo("en-US")),
                subtotal = subTotal.ToString("N", new CultureInfo("en-US")),
                tax = taxTotal.ToString("N", new CultureInfo("en-US"))
            };
        }

        #endregion



        public async Task<AuthToken> GetAccessToken(string host, string PayPalClientId, string Secret)
        {
            //Para proyectos .NET 4.0 se debe establecer específicamente TLS 1.2.  Se debe tener instalado el framework 4.5 para que esto funcione, 
            //de lo contrario restsharp siempre regresará 0 porque no se puede establecer el canal SSL/TLS
            System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072;
            var clientId = PayPalClientId;
            var secret = Secret;
            HttpClient http = new HttpClient
            {
                BaseAddress = new Uri(host),
                Timeout = TimeSpan.FromSeconds(30),
            };
            byte[] bytes = Encoding.GetEncoding("iso-8859-1").GetBytes($"{clientId}:{secret}");

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials"
            };

            request.Content = new FormUrlEncodedContent(form);
            AuthToken accessToken = new AuthToken();
            try
            {
                HttpResponseMessage response = await http.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();
                accessToken = JsonConvert.DeserializeObject<AuthToken>(content);
            }
            catch (TaskCanceledException etce)
            {
                _logger.Error("PayPalPlus IPN. error_ ", new NopException(etce.Message));
            }
            return accessToken;
        }

        private async Task<PayPalPaymentCreatedResponse> CreatePaypalPaymentAsync(string host, AuthToken accessToken, Customer customer, PayPalPlusPaymentSettings paypalPlusPaymentSettings, DetailsAmountInfo amountInfo, List<Item> items, decimal totalAdjust)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpClient http = new HttpClient
            {
                BaseAddress = new Uri(host),
                Timeout = TimeSpan.FromSeconds(30),
            };

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "v1/payments/payment");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Access_Token);
            request.Headers.Add("PayPal-Partner-Attribution-Id", "TecnofinPPP_Cart");
            // var model = new CreatePaymentModel();
            var shippingOption = _genericAttributeService.GetAttribute<ShippingOption>(customer, NopCustomerDefaults.SelectedShippingOptionAttribute, _storeContext.CurrentStore.Id);
            string shippingpreference = "SET_PROVIDED_ADDRESS";
            if (shippingOption == null || shippingOption.ShippingRateComputationMethodSystemName == "Pickup.PickupInStore")
                shippingpreference = "NO_SHIPPING";

            //get total discount amount
            var orderTotal = _orderTotalCalculationService.GetShoppingCartTotal(customer.ShoppingCartItems.ToList(),
                out decimal discountAmount,
                out List<DiscountForCaching> _, out List<AppliedGiftCard> _, out int _, out decimal _);

            if (discountAmount <= decimal.Zero)
                discountAmount = 0;
            var shippingRateComputationMethods = _shippingPluginManager
                        .LoadActivePlugins(customer, _storeContext.CurrentStore.Id);

            _orderTotalCalculationService.GetShoppingCartSubTotal(customer.ShoppingCartItems.ToList(), false,
                out decimal subdiscountAmount,
                out List<DiscountForCaching> subdiscountforCath, out decimal subTotalSinDescuento, out decimal subtotalConDescuento, out SortedDictionary<decimal, decimal> taxRates);
            var taxTotal = _orderTotalCalculationService.GetTaxTotal(customer.ShoppingCartItems.ToList(), shippingRateComputationMethods, true);
            var shipping = _orderTotalCalculationService.GetShoppingCartShippingTotal(customer.ShoppingCartItems.ToList(), shippingRateComputationMethods);
            var shippingTotal = shipping ?? 0;

            //List<RestObjects.Item> items = new List<RestObjects.Item>();
            //foreach (var ic in customer.ShoppingCartItems)
            //{
            //    var item = new RestObjects.Item
            //    {
            //        currency = paypalPlusPaymentSettings.Currency,
            //        name = ic.Product.Name,
            //        description = ic.Product.Name,
            //        quantity = ic.Quantity.ToString(),
            //        price = Math.Round(ic.Product.Price, 2).ToString(),
            //        sku = ic.Product.Sku,

            //    };
            //    items.Add(item);
            //}
            var itemList = new RestObjects.Items();
            itemList.items = items;
            if (shippingpreference == "SET_PROVIDED_ADDRESS")
            {
                var address = customer.ShippingAddress;
                itemList.shipping_address = new RestObjects.ShippingAddressInfo()
                {
                    city = address.City,
                    country_code = address.Country.TwoLetterIsoCode,
                    postal_code = address.ZipPostalCode,
                    phone = address.PhoneNumber,
                    state = address.StateProvince.Name,
                    recipient_name = "direccion",
                    line1 = address.Address1,
                    line2 = address.Address2,
                };
            }

            List<RestObjects.Transactions> transactions = new List<RestObjects.Transactions>();
            RestObjects.Transactions transaction = new RestObjects.Transactions()
            {
                amount = new RestObjects.AmountInfo
                {
                    currency = paypalPlusPaymentSettings.Currency,
                    details = amountInfo,
                    total = Math.Round(totalAdjust, 2).ToString(),
                    //details = new DetailsAmountInfo
                    //{
                    //    subtotal = Math.Round(subtotalConDescuento, 2).ToString(),
                    //    shipping = Math.Round(shippingTotal, 2).ToString(),
                    //    shipping_discount = Math.Round(discountAmount, 2).ToString(), // ver descuentos
                    //},
                    //total = Math.Round(orderTotal.Value, 2).ToString(),
                },

                description = "Compra en:" + _storeContext.CurrentStore.Name,
                custom = "Compra en:" + _storeContext.CurrentStore.Name,
                notify_url = new Uri(new Uri(_storeContext.CurrentStore.Url), "Plugins/PaymentPayPalPlus/IPNHandler").ToString(),
                payment_options = new RestObjects.PaymentOptions(),
                item_list = itemList,
            };
            transactions.Add(transaction);

            RestObjects.Payment paymentRest = new RestObjects.Payment
            {
                intent = "sale",
                application_context = new RestObjects.AplicationContext() { shipping_preference = shippingpreference },
                payer = new RestObjects.Payer(),
                transactions = transactions,
                redirect_urls = new RestObjects.RedirectUrlsInfo
                {
                    return_url = $"{_webHelper.GetStoreLocation()}Plugins/PaymentPayPalPlus/AprovalOrder",
                    cancel_url = $"{_webHelper.GetStoreLocation()}Plugins/PaymentPayPalPlus/CancelOrder",
                },
            };
            var data = Newtonsoft.Json.JsonConvert.SerializeObject(paymentRest);
            PayPalPaymentCreatedResponse paypalPaymentCreated = new PayPalPaymentCreatedResponse();
            try
            {
                request.Content = new StringContent(data, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await http.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();
                paypalPaymentCreated = JsonConvert.DeserializeObject<PayPalPaymentCreatedResponse>(content);
            }
            catch (TaskCanceledException etce)
            {
                _logger.Error("PayPalPlus IPN. error_ ", new NopException(etce.Message));
            }

            return paypalPaymentCreated;
        }

        public IViewComponentResult Invoke()
        {
            var model = new PaymentInfoModel();
            //load settings for a chosen store scope
            //ensure that we have 2 (or more) stores            
            var storeId = _storeContext.CurrentStore.Id;
            var store = _storeService.GetStoreById(storeId);
            var orderSettings = _settingService.LoadSetting<OrderSettings>(storeId);
            var payPalPlusPaymentSettings = _settingService.LoadSetting<PayPalPlusPaymentSettings>(storeId);

            string host = payPalPlusPaymentSettings.EnviromentSandBox;
            if (payPalPlusPaymentSettings.UseSandbox == false)
                host = payPalPlusPaymentSettings.EnviromentLive;

            //get current shopping cart
            var shoppingCart = _workContext.CurrentCustomer.ShoppingCartItems
                .Where(shoppingCartItem => shoppingCartItem.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .ToList();
            //.LimitPerStore(_storeContext.CurrentStore.Id).ToList();

            decimal totalAdjust = 0;
            //items
            var items = GetItems(shoppingCart, _workContext.CurrentCustomer, _storeContext.CurrentStore.Id, payPalPlusPaymentSettings.Currency);

            //amount details
            var amountDetails = GetAmountDetails(shoppingCart, items, out totalAdjust);

            try
            {
                Task.Run(async () =>
                {
                    AuthToken authToken = await GetAccessToken(host, payPalPlusPaymentSettings.ClientId, payPalPlusPaymentSettings.SecretId);

                    PayPalPaymentCreatedResponse createdPayment = await CreatePaypalPaymentAsync(host, authToken, _workContext.CurrentCustomer, payPalPlusPaymentSettings, amountDetails, items, totalAdjust);

                    var approval_url = createdPayment.links.FirstOrDefault(x => x.rel == "approval_url").href;
                    if (authToken != null)
                    {
                        // Guarda el apiContext
                        _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer, "authTokenPPP", Newtonsoft.Json.JsonConvert.SerializeObject(authToken), _storeContext.CurrentStore.Id);
                        // Guarda Pago
                        _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer, "createdPaymentPPP", Newtonsoft.Json.JsonConvert.SerializeObject(createdPayment), _storeContext.CurrentStore.Id);

                        model.Scriptppp = GetScriptPayment(_workContext.CurrentCustomer, authToken, createdPayment, storeId);
                    }
                    model.OnePageCheckoutEnabled = orderSettings.OnePageCheckoutEnabled;
                    model.Respuesta = authToken.Access_Token;
                    model.Error = false;
                }).Wait();
            }
            catch (Exception e)
            {
                model.Respuesta = e.Message;
                model.Error = true;
            }
            return View("~/Plugins/Payments.PayPalPlus/Views/PaymentInfo.cshtml", model);
        }

        protected string GetScriptPayment(Customer customer, AuthToken authToken, PayPalPaymentCreatedResponse createdPayment, int storeId)
        {
            var payPalPlusPaymentSettings = _settingService.LoadSetting<PayPalPlusPaymentSettings>(storeId);
            string mode = "sandbox";

            if (payPalPlusPaymentSettings.UseSandbox == false)
                mode = "Live";

            
            string scriptppp = _localizationService.GetResource("Plugins.Payments.PayPalPlus.Fields.scriptOne");
            if (!_orderSettings.OnePageCheckoutEnabled)
                scriptppp = _localizationService.GetResource("Plugins.Payments.PayPalPlus.Fields.scriptPage");

            scriptppp = scriptppp.Replace("{UrlTiendaAproval}", createdPayment.links.FirstOrDefault(x => x.rel == "approval_url").href);
            scriptppp = scriptppp.Replace("{Country}", payPalPlusPaymentSettings.CountryTwoLetters);
            scriptppp = scriptppp.Replace("{PayerEmail}", customer.Email);
            scriptppp = scriptppp.Replace("{PayerFirstName}", _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.FirstNameAttribute));  
            scriptppp = scriptppp.Replace("{PayerLastName}", _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.LastNameAttribute));
            scriptppp = scriptppp.Replace("{PayerPhone}", _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.PhoneAttribute));
            scriptppp = scriptppp.Replace("{Mode}", mode);
            scriptppp = scriptppp.Replace("{DisallowRememberedCards}", payPalPlusPaymentSettings.DisallowRememberedCards.ToString().ToLower());
            scriptppp = scriptppp.Replace("{Language}", payPalPlusPaymentSettings.Language);
            scriptppp = scriptppp.Replace("{RCards}", _genericAttributeService.GetAttribute<string>(customer, "PPPTokenCards"));
            scriptppp = scriptppp.Replace("{IFrameHeight}", payPalPlusPaymentSettings.IFrameHeight + "px");
            scriptppp = scriptppp.Replace("\r\n", "");
            return scriptppp;
        }
    }
}