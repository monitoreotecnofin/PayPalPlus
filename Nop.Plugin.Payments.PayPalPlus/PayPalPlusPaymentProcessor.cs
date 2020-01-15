using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.PayPalPlus.Controllers;
using Nop.Plugin.Payments.PayPalPlus.RestObjects;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Tax;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Routing;

namespace Nop.Plugin.Payments.PayPalPlus
{
    public class PayPalPlusPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields
        private readonly CurrencySettings _currencySettings;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICurrencyService _currencyService;
        private readonly IGenericAttributeService _genericAttributeService;        
        private readonly ILocalizationService _localizationService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly ISettingService _settingService;
        private readonly ITaxService _taxService;
        private readonly IWebHelper _webHelper;
        private readonly PayPalPlusPaymentSettings _paypalPlusPaymentSettings;        
        private readonly IWorkContext _workContext;
        private readonly ILogger _logger;
        private readonly IStoreContext _storeContext;
        #endregion
        #region Ctor
        public PayPalPlusPaymentProcessor(CurrencySettings currencySettings,
            ICheckoutAttributeParser checkoutAttributeParser,
            ICurrencyService currencyService,
            IGenericAttributeService genericAttributeService,            
            ILocalizationService localizationService,
            IOrderTotalCalculationService orderTotalCalculationService,
            ISettingService settingService,
            ITaxService taxService,
            IWebHelper webHelper,
            PayPalPlusPaymentSettings paypalPlusPaymentSettings, IWorkContext workContext,
            ILogger logger,
            IStoreContext storeContext)
        {
            this._currencySettings = currencySettings;
            this._checkoutAttributeParser = checkoutAttributeParser;
            this._currencyService = currencyService;
            this._genericAttributeService = genericAttributeService;            
            this._localizationService = localizationService;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._settingService = settingService;
            this._taxService = taxService;
            this._webHelper = webHelper;
            this._paypalPlusPaymentSettings = paypalPlusPaymentSettings;
            this._workContext = workContext;
            this._logger = logger;
            this._storeContext = storeContext;
        }
        #endregion
        #region Properties
        public bool SupportCapture
        {
            get { return false; }
        }

        public bool SupportPartiallyRefund
        {
            get { return false; }
        }

        public bool SupportRefund
        {
            get { return false; }
        }

        public bool SupportVoid
        {
            get { return false; }
        }

        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Standard; }
        }

        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        public string PaymentMethodDescription
        {
            //return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
            //for example, for a redirection payment method, description may be like this: "You will be redirected to PayPal site to complete the payment"
            get { return _localizationService.GetResource("Plugins.Payments.PayPalPlus.PaymentMethodDescription");}
        }
        #endregion
        #region Methods
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            var payPalPlusPaymentSettings = _settingService.LoadSetting<PayPalPlusPaymentSettings>(processPaymentRequest.StoreId);
            // confirmar orden 
            var customer = _workContext.CurrentCustomer;
            string accessTokenppp = customer.GetAttribute<string>("authTokenPPP", processPaymentRequest.StoreId);
            var authToken = Newtonsoft.Json.JsonConvert.DeserializeObject<AuthToken>(accessTokenppp);

            string createdPaymentppp = customer.GetAttribute<string>("createdPaymentPPP", processPaymentRequest.StoreId);
            var createdPayment = Newtonsoft.Json.JsonConvert.DeserializeObject<PayPalPaymentCreatedResponse>(createdPaymentppp);
            
            string payerId = string.Empty;
            foreach(var value in processPaymentRequest.CustomValues)
            {
                if(value.Key == "PaypalPayerId")
                {
                    payerId = value.Value.ToString();
                    break;
                }
            }            
            //processPaymentRequest.CustomValues.TryGetValue("PaypalPayerId", out object payerId);
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string urlipn = new Uri(new Uri(_storeContext.CurrentStore.Url), "Plugins/PaymentPayPalPlus/IPNHandler").ToString();

            try
            {
                Task.Run(async () =>
                {
                    string host = _paypalPlusPaymentSettings.EnviromentSandBox;
                    if (_paypalPlusPaymentSettings.UseSandbox == false)
                        host = _paypalPlusPaymentSettings.EnviromentLive;

                    var http = new HttpClient
                    {
                        BaseAddress = new Uri(host),
                        Timeout = TimeSpan.FromSeconds(30),
                    };
                    try
                    { 
                        var patchPayment = await PatchPaypalPaymentAsync(http, authToken, createdPayment.id, payerId.ToString(), processPaymentRequest, payPalPlusPaymentSettings, urlipn);
                    }
                    catch (TaskCanceledException expatch)
                    {
                        result.AddError("Failed to Patch to PalPalPlus" + expatch.Message);
                        _logger.Error("PayPalPlus Patch. error: ", new NopException(expatch.Message));
                    }

                    try
                    { 
                        PayPalPaymentExecutedResponse executedPayment = await ExecutePaypalPaymentAsync(http, authToken, createdPayment.id, payerId.ToString(), processPaymentRequest, payPalPlusPaymentSettings, urlipn);
                        if(executedPayment != null)
                        {
                            if (executedPayment.StatusCode == "200")
                            {
                                if (executedPayment.transactions != null)
                                {
                                    if (executedPayment.transactions[0].related_resources[0].sale.state != "completed" && executedPayment.transactions[0].related_resources[0].sale.state != "pending")
                                    {
                                        result.AddError(_localizationService.GetResource("Plugins.Payments.PayPalPlus.ErrorTarjeta"));
                                    }
                                }
                            }
                            else
                            {
                                result.AddError(_localizationService.GetResource("Plugins.Payments.PayPalPlus.ErrorTarjetaNoAprovada"));
                            }
                        }
                        else
                        {
                            result.AddError(_localizationService.GetResource("Plugins.Payments.PayPalPlus.ErrorTarjetaNoAprovada"));
                        }
                    }
                    catch (TaskCanceledException exExe)
                    {
                        result.AddError("Failed to Executed to PalPalPlus" + exExe.Message);
                        _logger.Error("PayPalPlus Executed. error ", new NopException(exExe.Message));
                    }
                }).Wait();
            }
            catch (Exception ex)
            {
                result.AddError("Failed to send to PalPalPlus" + ex.Message);                
            }
            return result;
        }

        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
                       
        }

        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            return false;
        }

        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return this.CalculateAdditionalFee(_orderTotalCalculationService, cart,
                _paypalPlusPaymentSettings.AdditionalFee, _paypalPlusPaymentSettings.AdditionalFeePercentage);
        }

        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult { Errors = new[] { "Capture method not supported" } };
        }

       public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            return new RefundPaymentResult { Errors = new[] { "Refund method not supported" } };
        }

        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            return new VoidPaymentResult { Errors = new[] { "Void method not supported" } };
        }

        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

       public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

       public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(order.GetType().ToString());

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }

        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentPayPalPlus";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.PayPalPlus.Controllers" }, { "area", null } };

        }

        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentPayPalPlus";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.PayPalPlus.Controllers" }, { "area", null } };
        }
        public Type GetControllerType()
        {
            return typeof(PaymentPayPalPlusController);
        }

         /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            #region scriptOnePage
            string scriptOne = @"<script type='text/javascript'>
                $.getScript('https://www.paypalobjects.com/webstatic/ppplusdcc/ppplusdcc.min.js?ver=3.1.2',
                function(data, textStatus, jqxhr) {                
                $('#opc-payment_info .payment-info-next-step-button').attr('id', 'continueButton');                
                var ppp = PAYPAL.apps.PPP({                
                approvalUrl: '{UrlTiendaAproval}',
                placeholder: 'pppDiv',
                country: '{Country}',                
                payerEmail: '{PayerEmail}',
                payerPhone: '{PayerPhone}',
                payerFirstName: '{PayerFirstName}',
                payerLastName: '{PayerLastName}',
                payerTaxId: '',                
                mode: '{Mode}',
                language: '{Language}',
                hideMxDebitCards: false,
                useraction: 'continue',
                buttonLocation: 'outside',
                disableContinue: 'continueButton',
                enableContinue: 'continueButton',
                preselection: 'none',                
                merchantInstallmentSelectionOptional: true,                
                disallowRememberedCards: {DisallowRememberedCards},
                rememberedCards: '{RCards}',
                surcharging: false,
                hideAmount: false,
                iframeHeight: '{IFrameHeight}',
                onContinue: function(rememberedCards, payerId, token, term) 
                            {                            
                                $('#payerId').val(payerId); 
                                if(rememberedCards) 
                                {
                                    $('#payerTokenCards').val(rememberedCards);
                                }
                                $('#continueButton').addClass('hidden');                        
                                PaymentInfo.save();
                            }
                }); 
                $('#opc-payment_info .payment-info-next-step-button').removeAttr('onclick').click(function() {
                    ppp.doContinue(); });
            });
            </script> ";
            #endregion

            #region scriptPageToPage
            string boton = @"<input name='nextstep' id='nextstep2' type ='submit' value='Next' class='button-1 payment-info-next-step-button'>";
            string scriptPage = @"<script type='text/javascript'>
                $.getScript('https://www.paypalobjects.com/webstatic/ppplusdcc/ppplusdcc.min.js?ver=3.1.2',
                function(data, textStatus, jqxhr) {
                    $('.payment-info-next-step-button').attr('id', 'nextstep');
                    $('.payment-info form').attr('id', 'paymentppp');
                    $('.payment-info form').attr('name', 'paymentppp');                    
                    var ppp = PAYPAL.apps.PPP({
                        approvalUrl: '{UrlTiendaAproval}',
                        placeholder: 'pppDiv',
                        country: '{Country}',
                        payerEmail: '{PayerEmail}',
                        payerPhone: '{PayerPhone}',
                        payerFirstName: '{PayerFirstName}',
                        payerLastName: '{PayerLastName}',
                        payerTaxId: '',
                        mode: '{Mode}',
                        language: '{Language}',
                        hideMxDebitCards: false,
                        useraction: 'continue',
                        buttonLocation: 'outside',
                        disableContinue: 'nextstep',
                        enableContinue: 'nextstep',
                        preselection: 'none',
                        merchantInstallmentSelectionOptional: true,
                        disallowRememberedCards: {DisallowRememberedCards},
                        rememberedCards: '{RCards}',
                        surcharging: false,
                        hideAmount: false,
                        iframeHeight: '{IFrameHeight}',
                        onContinue: function(rememberedCards, payerId, token, term)
                        {                       
                            console.log('payerID:' + payerId);
                            console.log('rememberedCards:' + rememberedCards);
                            $('#payerId').val(payerId);
                            if(rememberedCards)                            
                                $('#payerTokenCards').val(rememberedCards);
                            $(""" + boton + @""").appendTo('#paymentppp');
                            $('#nextstep2').trigger('click');
                        }
                     }); 
                    $(document).ready(function () {
                        $('#nextstep').click(function() {
                        if (typeof(ppp) != 'undefined')
                        {
                            ppp.doContinue();
                        }
                        else
                        {
                            alert('ppp no está definido.');
                        }
                        return false;
                    });
                  });    
                });
                </script>";
            #endregion
            //settings
            _settingService.SaveSetting(new PayPalPlusPaymentSettings
            {
                UseSandbox = true,              
		Name = "PayPalTienda",
                ClientId = "ClientId",
                SecretId = "SecretId",
                EnviromentLive = "https://api.paypal.com",
                EnviromentSandBox = "https://api.sandbox.paypal.com",
                DisallowRememberedCards =  false,
                Language = "es_MX",
                IFrameHeight = "350",
                AdditionalFee = 0,
                AdditionalFeePercentage= false,
                Currency = "MXN",
                CountryTwoLetters = "MX"
            });

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Name", "Nombre Id para PayPal.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Name.Hint", "Nombre Id para PayPal sin espacios y sin caracteres especiales");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ClientId", "IdCliente");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ClientId.Hint", "Id de cliente paypal se obtiene de la consola");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.SecretId", "IdSecret");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.SecretId.Hint", "Palabra secreta de paypal");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.EnviromentSandBox", "Ambiente de SandBox");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.EnviromentSandBox.Hint", "Ambiente o link de sandbox");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.EnviromentLive", "Ambiente productivo");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.EnviromentLive.Hint", "Ambiente de produccion");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Language", "Idioma");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Language.Hint", "Codigo de Idioma del pais, revizar compatibilidad en PayPalPlus");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.IFrameHeight", "Altura Iframe");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.IFrameHeight.Hint", "Altura de Iframe");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.DisallowRememberedCards", "Deshabilita recordar tarjetas");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.DisallowRememberedCards.Hint", "Deshabilita recordar tarjetas");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.AdditionalFee", "Cargo adicional.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.AdditionalFee.Hint", "Introduce el cargo adicional para los clientes.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.AdditionalFeePercentage", "Cargo adicional. Usar porcentaje.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.AdditionalFeePercentage.Hint", "Determina si usar un monto adicional o usar un porcentaje al total del pedido.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.RedirectionTip", "Pagos Seguros con Tarjetas Visa, MasterCard, American Express.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.UseSandbox", "Usar Sandbox");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.UseSandbox.Hint", "Habilitar Sandbox (Ambiente de prueba).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Instructions", "<p><b>Si estás utilizando este metodo de cobro, asegúrate de que la moneda de la tienda principal sea compatible con PayPal.</b><br /><br />Guía rápida para obtener credenciales de API REST: <br /><br /><b>1. Ingresar a la página de PayPal.</b><br />Para poderse identificar con las API’s de PayPal Plus será necesario primeramente crear un App Client ID y Secret los cuales podrán ser creados en la siguiente página: http://developer.paypal.com en la sección de DASHBOARD -> MyApps.<br />(*Debe contar con una cuenta de PayPal para ingresar a este sitio.)<br /><b>2. Crear App.</b><br />Después de hacer clic en Create App, se mostrará esta pantalla en la cual se deberá ingresar un App Name y seleccionar un correo para Sandbox.<br /><b>3. Obtener credenciales (Client ID y Secret).</b><br />Al completar los pasos se mostrarán el Client ID y el Secret . Por favor tome en cuenta que debe seleccionar las credenciales que correspondan al ambiente de pruebas (Sandbox) o producción (Live) según corresponda a el ambiente que esté integrando.</p><p>Para descargar el manual click <a href=\"{0}\" target =\"_blank\">aquí</a></p>");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.PaymentMethodDescription", "Pagos Seguros con Visa, MasterCard, o American Express.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.RoundingWarning", "Se refleja si se tiene \"ShoppingCartSettings.RoundPricesDuringCalculation\" la configuración deshabilitado. Ten en cuenta que esto puede ocasionar una discrepancia en el importe total del pedido, ya que PayPal solo redondea a dos decimales.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutOnePage", "Script de PayPal Plus para NopCommerce OnePage");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutOnePage.Hint", "Script de PayPal Plus para NopCommerce OnePage Reviza Clases y Botones");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutPageToPage", "Script de PayPal Plus para NopCommerce Página a Página");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutPageToPage.Hint", "Script de PayPal Plus para NopCommerce Página a Página Reviza Clases y Botones");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Currency", "Moneda que acepta paypal");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Currency.Hint", "Moneda que acepta paypal MXN para méxico");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.scriptOne", scriptOne);
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.scriptPage", scriptPage);
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.CountryTwoLetters", "País");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.CountryTwoLetters.Hint", "País 2 letras codigo ISO");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.ErrorTarjeta", "No es posible completar la compra, por favor intenta con otra tarjeta.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.ErrorTarjetaNoAprovada", "No es posible completar la compra, su tarjeta fue rechazada por el banco emisor.");
            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<PayPalPlusPaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Name");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Name.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ClientId");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ClientId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.SecretId");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.SecretId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.EnviromentSandBox");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.EnviromentSandBox.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.EnviromentLive");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.EnviromentLive.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Language");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Language.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.IFrameHeight");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.IFrameHeight.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.DisallowRememberedCards");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.DisallowRememberedCards.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.AdditionalFee.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.AdditionalFeePercentage");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.AdditionalFeePercentage.Hint");            
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.RedirectionTip");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.UseSandbox");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.UseSandbox.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Instructions");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.PaymentMethodDescription");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.RoundingWarning");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutOnePage");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutOnePage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutPageToPage");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutPageToPage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Currency");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Currency.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.scriptOne");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.scriptPage");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.CountryTwoLetters.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.CountryTwoLetters");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.ErrorTarjeta");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.ErrorTarjetaNoAprovada");
            base.Uninstall();
        }

        #endregion       
        #region Tools        
        private static async Task<string> PatchPaypalPaymentAsync(HttpClient http, AuthToken accessToken, string paymentId, string payerId, ProcessPaymentRequest processPaymentRequest, PayPalPlusPaymentSettings paypalPlusPaymentSettings, string urlnofications)
        {
            var method = new HttpMethod("PATCH");
            string patchorder = "v1/payments/payment/" + paymentId;
            HttpRequestMessage request = new HttpRequestMessage(method, patchorder);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Access_Token);

            List<Object> cambios = new List<Object>();
            Object invoice = new {
                op = "add",
                path = "/transactions/0/invoice_number",
                value = processPaymentRequest.OrderGuid.ToString()                      
            };           
            Object amount = new
            {
                op = "replace",
                path = "/transactions/0/amount",
                value = new
                {
                    total = Math.Round(processPaymentRequest.OrderTotal, 2).ToString(),
                    currency = paypalPlusPaymentSettings.Currency,
                    details = new
                    {
                        subtotal = Math.Round(processPaymentRequest.OrderTotal, 2).ToString()
                    }
                }
            };
            cambios.Add(invoice);
            cambios.Add(amount);
            string contentr = string.Empty;            
            var content = JsonConvert.SerializeObject(cambios);
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await http.SendAsync(request);
            contentr = await response.Content.ReadAsStringAsync();
            
            return contentr;
        }

        private static async Task<PayPalPaymentExecutedResponse> ExecutePaypalPaymentAsync(HttpClient http, AuthToken accessToken, string paymentId, string payerId, ProcessPaymentRequest processPaymentRequest, PayPalPlusPaymentSettings paypalPlusPaymentSettings, string urlnofications)
        {
            string executeorder = "v1/payments/payment/" + paymentId + "/execute";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, executeorder);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Access_Token);
            request.Headers.Add("PayPal-Partner-Attribution-Id", "TecnofinPPP_Cart");
            List<RestObjects.Transactions> transacciones = new List<RestObjects.Transactions>();
            RestObjects.Transactions transaccion = new RestObjects.Transactions()
            {
                amount = new RestObjects.AmountInfo
                {
                    currency = paypalPlusPaymentSettings.Currency,
                    details = new DetailsAmountInfo
                    {
                        subtotal = Math.Round(processPaymentRequest.OrderTotal, 2).ToString()
                    },
                    total = Math.Round(processPaymentRequest.OrderTotal, 2).ToString(),
                },                
                notify_url = urlnofications,                
            };
            transacciones.Add(transaccion);

            var payment = JObject.FromObject(new
            {
                payer_id = payerId,
                transactions = transacciones
            });

            request.Content = new StringContent(JsonConvert.SerializeObject(payment), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await http.SendAsync(request); 
            
            string content = await response.Content.ReadAsStringAsync();
            PayPalPaymentExecutedResponse executedPayment = JsonConvert.DeserializeObject<PayPalPaymentExecutedResponse>(content);
            if (Convert.ToInt16(response.StatusCode) == 200)
                executedPayment.StatusCode = "200";
            else
                executedPayment.StatusCode = "400";
            return executedPayment;
        }
        #endregion

    }
}
