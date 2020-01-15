using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Localization;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.PayPalPlus.RestObjects;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Tax;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using System.Globalization;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Linq;

namespace Nop.Plugin.Payments.PayPalPlus
{
    public class PayPalPlusPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields
        private readonly CurrencySettings _currencySettings;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICurrencyService _currencyService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly ISettingService _settingService;
        private readonly ITaxService _taxService;
        private readonly IWebHelper _webHelper;
        private readonly PayPalPlusPaymentSettings _paypalPlusPaymentSettings;
        private readonly ICustomerService _customerService;
        private readonly IWorkContext _workContext;
        private readonly ILogger _logger;
        private readonly IStoreContext _storeContext;
        private readonly IProductAttributeParser _productAttributeParser;
        private readonly IPriceCalculationService _priceCalculationService;
        private readonly IPaymentService _paymentService;
        private readonly OrderSettings _orderSettings;
        #endregion

        #region Ctor
        public PayPalPlusPaymentProcessor(CurrencySettings currencySettings,
            ICheckoutAttributeParser checkoutAttributeParser,
            ICurrencyService currencyService,
            IGenericAttributeService genericAttributeService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            IOrderTotalCalculationService orderTotalCalculationService,
            ISettingService settingService,
            ITaxService taxService,
            IWebHelper webHelper,
            PayPalPlusPaymentSettings paypalPlusPaymentSettings, IWorkContext workContext,
            ILogger logger,
            IStoreContext storeContext, ICustomerService customerService,
            IProductAttributeParser productAttributeParser,
            IPriceCalculationService priceCalculationService,
            IPaymentService paymentService,
            OrderSettings orderSettings)
        {
            this._currencySettings = currencySettings;
            this._checkoutAttributeParser = checkoutAttributeParser;
            this._currencyService = currencyService;
            this._genericAttributeService = genericAttributeService;
            this._httpContextAccessor = httpContextAccessor;
            this._localizationService = localizationService;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._settingService = settingService;
            this._taxService = taxService;
            this._webHelper = webHelper;
            this._paypalPlusPaymentSettings = paypalPlusPaymentSettings;
            this._workContext = workContext;
            this._logger = logger;
            this._storeContext = storeContext;
            this._customerService = customerService;
            this._productAttributeParser = productAttributeParser;
            this._priceCalculationService = priceCalculationService;
            this._paymentService = paymentService;
            this._orderSettings = orderSettings;
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
            get
            {
                return _localizationService.GetResource("Plugins.Payments.PayPalPlus.PaymentMethodDescription");
            }
        }
        #endregion
        #region Methods
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }

        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult { Errors = new[] { "Capture method not supported" } };
        }

        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return _paymentService.CalculateAdditionalFee(cart,
               _paypalPlusPaymentSettings.AdditionalFee, _paypalPlusPaymentSettings.AdditionalFeePercentage);           
        }

        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            ProcessPaymentRequest paymentRequest = new ProcessPaymentRequest();
            if (form.TryGetValue("payerId", out StringValues payerId) && !StringValues.IsNullOrEmpty(payerId))
                paymentRequest.CustomValues.Add("PaypalPayerId", payerId.ToString());

            if (form.TryGetValue("payerTokenCards", out StringValues payerTokenCards))
            {
                if (!StringValues.IsNullOrEmpty(payerTokenCards))
                    _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer, "PPPTokenCards", payerTokenCards, _storeContext.CurrentStore.Id);
            }


            return paymentRequest;
        }
        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentPayPalPlus/Configure";
        }

        public string GetPublicViewComponentName()
        {
            return "PaymentPayPalPlus";
        }        

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            #region scriptOnePage
            string scriptOne = @"<script type='text/javascript'>
                        $(document).ready(function () {   
                            $('.payment-info-next-step-button').attr('onclick', null);
                            $('.payment-info-next-step-button').prop('disabled', true);
                            $('.payment-info-next-step-button').css('opacity', .4);
                        $('.payment-info-next-step-button').attr('id', 'continueButton');
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

          jQuery('.payment-info-next-step-button').attr('onclick', 'PaymentInfo.save();');
                       jQuery('.payment-info-next-step-button').prop('disabled', false);
                        jQuery('.payment-info-next-step-button').css('opacity', 1);
                        PaymentInfo.save();
                    },
                    onError: function (err) {
                        var msg = jQuery('#responseOnError').html() + '<BR />' + JSON.stringify(err);
                        jQuery('#responseOnError').html(msg);
                    },
                    onLoad: function (err) {
                        jQuery('#pppDiv iframe').width('100%');
                        jQuery('#pppDiv iframe').height(550);
                        jQuery('#pppDiv').width('100%');           
              jQuery('#pppDiv').height(550);

             jQuery('#pppPaidBotton').click( function()
               {
                    ppp.doContinue();
                }
              );
                }
            });

          $(document).on('accordion_section_opened', function (data) {
                if (data && (data.currentSectionId == 'opc-billing' || data.currentSectionId == 'opc-shipping' || data.currentSectionId == 'opc-shipping_method' || data.currentSectionId == 'opc-payment_method'))
                {
                            $('.payment-info-next-step-button').prop('disabled', false);
                        $('.payment-info-next-step-button').css('opacity', 1);
                $('.payment-info-next-step-button').attr('onclick', 'PaymentInfo.save();');
                }
            });

            });
            </script> ";
            #endregion

            #region scriptPageToPage

            string scriptPage = @"<script type='text/javascript'>  
              $(document).ready(function () {
                    $('.payment-info-next-step-button').attr('onclick', null);
                    $('.payment-info-next-step-button').prop('disabled', true);
                    $('.payment-info-next-step-button').css('opacity', .4);
            $('.payment-info-next-step-button').attr('type',null);
                $('.payment-info-next-step-button').attr('id', 'continueButton');
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
             jQuery('.payment-info-next-step-button').prop('disabled', false);
                jQuery('.payment-info-next-step-button').css('opacity', 1);
               jQuery('.payment-info-next-step-button').attr('type','submit');
            jQuery('.payment-info-next-step-button').trigger('click');
                        },
                        onError: function (err) {
                            var msg = jQuery('#responseOnError').html() + '<BR />' + JSON.stringify(err);
                            jQuery('#responseOnError').html(msg);
                        },
                        onLoad: function (err) {
                            jQuery('#pppDiv iframe').width('100%');
                            jQuery('#pppDiv iframe').height(550);
                            jQuery('#pppDiv').width('100%');           
              jQuery('#pppDiv').height(550);
             jQuery('#pppPaidBotton').click(function()
                       {
                            ppp.doContinue();
                        }
                  );
                    }
                });
             });   
            </script>";
            #endregion
            //settings
            _settingService.SaveSetting(new PayPalPlusPaymentSettings
            {
                UseSandbox = true,
                //Name = "PayPalOfix",
                //ClientId = "ATY9iVJeNwS03THVUpU13tYwOWszXHK7rf-DiMhIK24rNL6o-bRH-5Uzyc9gq3LS0Jhppx4miLNzNhP1",
                //SecretId = "EF5YkR-aElzVt8X9OVpk5IbL5ju3pbrpE5-VzeJmSrFDapNXQtKOYGjsfRzIwOyOFAbC8gbIfQE_f7IZ",
                Name = "PayPalTienda",
                ClientId = "ClienteId",
                SecretId = "SecretId",
                EnviromentLive = "https://api.paypal.com",
                EnviromentSandBox = "https://api.sandbox.paypal.com",
                DisallowRememberedCards = false,
                Language = "es_MX",
                IFrameHeight = "350",
                AdditionalFee = 0,
                AdditionalFeePercentage = false,
                Currency = "MXN",
                CountryTwoLetters = "MX"
            });

            //locales
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Name", "Nombre Id para PayPal.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Name.Hint", "Nombre Id para PayPal sin espacios y sin caracteres especiales");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ClientId", "IdCliente");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ClientId.Hint", "Id de cliente paypal se obtiene de la consola");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.SecretId", "IdSecret");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.SecretId.Hint", "Palabra secreta de paypal");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.EnviromentSandBox", "Ambiente de SandBox");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.EnviromentSandBox.Hint", "Ambiente o link de sandbox");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.EnviromentLive", "Ambiente productivo");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.EnviromentLive.Hint", "Ambiente de produccion");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Language", "Idioma");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Language.Hint", "Codigo de Idioma del pais, revizar compatibilidad en PayPalPlus");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.IFrameHeight", "Altura Iframe");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.IFrameHeight.Hint", "Altura de Iframe");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.DisallowRememberedCards", "Deshabilita recordar tarjetas");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.DisallowRememberedCards.Hint", "Deshabilita recordar tarjetas");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.AdditionalFee", "Cargo adicional.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.AdditionalFee.Hint", "Introduce el cargo adicional para los clientes.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.AdditionalFeePercentage", "Cargo adicional. Usar porcentaje.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.AdditionalFeePercentage.Hint", "Determina si usar un monto adicional o usar un porcentaje al total del pedido.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.RedirectionTip", "Pagos Seguros con Tarjetas Visa, MasterCard, American Express.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.UseSandbox", "Usar Sandbox");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.UseSandbox.Hint", "Habilitar Sandbox (Ambiente de prueba).");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Instructions", "<p><b>Si estás utilizando este metodo de cobro, asegúrate de que la moneda de la tienda principal sea compatible con PayPal.</b><br /><br />Guía rápida para obtener credenciales de API REST: <br /><br /><b>1. Ingresar a la página de PayPal.</b><br />Para poderse identificar con las API’s de PayPal Plus será necesario primeramente crear un App Client ID y Secret los cuales podrán ser creados en la siguiente página: http://developer.paypal.com en la sección de DASHBOARD -> MyApps.<br />(*Debe contar con una cuenta de PayPal para ingresar a este sitio.)<br /><b>2. Crear App.</b><br />Después de hacer clic en Create App, se mostrará esta pantalla en la cual se deberá ingresar un App Name y seleccionar un correo para Sandbox.<br /><b>3. Obtener credenciales (Client ID y Secret).</b><br />Al completar los pasos se mostrarán el Client ID y el Secret . Por favor tome en cuenta que debe seleccionar las credenciales que correspondan al ambiente de pruebas (Sandbox) o producción (Live) según corresponda a el ambiente que esté integrando.</p><p>Para descargar el manual click <a href=\"{0}\" target =\"_blank\">aquí</a></p>");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.PaymentMethodDescription", "Pagos Seguros con Visa, MasterCard, o American Express.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.RoundingWarning", "Se refleja si se tiene \"ShoppingCartSettings.RoundPricesDuringCalculation\" la configuración deshabilitado. Ten en cuenta que esto puede ocasionar una discrepancia en el importe total del pedido, ya que PayPal solo redondea a dos decimales.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutOnePage", "Script de PayPal Plus para NopCommerce OnePage");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutOnePage.Hint", "Script de PayPal Plus para NopCommerce OnePage Reviza Clases y Botones");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutPageToPage", "Script de PayPal Plus para NopCommerce Página a Página");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutPageToPage.Hint", "Script de PayPal Plus para NopCommerce Página a Página Reviza Clases y Botones");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Currency", "Moneda que acepta paypal");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Currency.Hint", "Moneda que acepta paypal MXN para méxico");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.scriptOne", scriptOne);
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.scriptPage", scriptPage);
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.CountryTwoLetters", "País");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.CountryTwoLetters.Hint", "País 2 letras codigo ISO");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.ErrorTarjeta", "No es posible completar la compra, por favor regresa a la forma de pago e intenta con otra tarjeta.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.ErrorTarjetaNoAprovada", "No es posible completar la compra, su tarjeta fue rechazada por el banco emisor, por favor regresa a la forma de pago e intenta con otra tarjeta.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalPlus.IntentarNuevamente", "Intentar nuevamente con otra tarjeta.");
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
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Name");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Name.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ClientId");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ClientId.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.SecretId");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.SecretId.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.EnviromentSandBox");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.EnviromentSandBox.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.EnviromentLive");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.EnviromentLive.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Language");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Language.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.IFrameHeight");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.IFrameHeight.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.DisallowRememberedCards");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.DisallowRememberedCards.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.AdditionalFee");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.AdditionalFee.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.AdditionalFeePercentage");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.AdditionalFeePercentage.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.RedirectionTip");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.UseSandbox");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.UseSandbox.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Instructions");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.PaymentMethodDescription");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.RoundingWarning");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutOnePage");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutOnePage.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutPageToPage");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutPageToPage.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Currency");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.Currency.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.scriptOne");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.scriptPage");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.CountryTwoLetters.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.Fields.CountryTwoLetters");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.ErrorTarjeta");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.ErrorTarjetaNoAprovada");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayPalPlus.IntentarNuevamente");
            base.Uninstall();
        }

        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            // you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Gets IPN PayPal URL
        /// </summary>
        /// <returns></returns>
        private string GetIpnPaypalUrl()
        {
            return _paypalPlusPaymentSettings.UseSandbox ?
                "https://ipnpb.sandbox.paypal.com/cgi-bin/webscr" :
                "https://ipnpb.paypal.com/cgi-bin/webscr";
        }

        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {

        }

        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            var payPalPlusPaymentSettings = _settingService.LoadSetting<PayPalPlusPaymentSettings>(processPaymentRequest.StoreId);
            // confirmar orden 
            var customer = _workContext.CurrentCustomer;
            string accessTokenppp = _genericAttributeService.GetAttribute<string>(customer, "authTokenPPP", _storeContext.CurrentStore.Id);//customer.GetAttribute<string>("authTokenPPP", processPaymentRequest.StoreId);
            var authToken = Newtonsoft.Json.JsonConvert.DeserializeObject<AuthToken>(accessTokenppp);

            string createdPaymentppp = _genericAttributeService.GetAttribute<string>(customer,"createdPaymentPPP", _storeContext.CurrentStore.Id);
            var createdPayment = Newtonsoft.Json.JsonConvert.DeserializeObject<PayPalPaymentCreatedResponse>(createdPaymentppp);
            processPaymentRequest.CustomValues.TryGetValue("PaypalPayerId", out object payerId);
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string urlipn = new Uri(new Uri(_storeContext.CurrentStore.Url), "Plugins/PaymentPayPalPlus/IPNHandler").ToString();

            //get current shopping cart
            var shoppingCart = _workContext.CurrentCustomer.ShoppingCartItems
                .Where(shoppingCartItem => shoppingCartItem.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .LimitPerStore(_storeContext.CurrentStore.Id).ToList();
            var items = GetItems(shoppingCart, customer, processPaymentRequest.StoreId, _paypalPlusPaymentSettings.Currency);

            //amount details
            var amountDetails = GetAmountDetails(processPaymentRequest, shoppingCart, items);

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

                    string ErrorRechazo = _localizationService.GetResource("Plugins.Payments.PayPalPlus.ErrorTarjeta");
                    string ErrorNoAprovada = _localizationService.GetResource("Plugins.Payments.PayPalPlus.ErrorTarjetaNoAprovada");

                    try
                    {
                        string patchPayment = await PatchPaypalPaymentAsync(http, authToken, createdPayment.id, payerId.ToString(), processPaymentRequest, payPalPlusPaymentSettings, urlipn);
                        if (patchPayment == "400")
                            result.AddError("Failed to Patch Invoice to PalPalPlus");
                    }
                    catch (TaskCanceledException expatch)
                    {
                        result.AddError("Failed to Patch to PalPalPlus" + expatch.Message);
                        _logger.Error("PayPalPlus Patch. error: ", new NopException(expatch.Message));
                    }

                    try
                    {
                        PayPalPaymentExecutedResponse executedPayment = await ExecutePaypalPaymentAsync(http, authToken, createdPayment.id, payerId.ToString(), processPaymentRequest, payPalPlusPaymentSettings, urlipn, customer.ShoppingCartItems.ToList(), _orderTotalCalculationService, amountDetails);
                        if (executedPayment != null)
                        {
                            if (executedPayment.StatusCode == "200")
                            {
                                if (executedPayment.transactions != null)
                                {
                                    if (executedPayment.transactions[0].related_resources[0].sale.state != "completed" && executedPayment.transactions[0].related_resources[0].sale.state != "pending")
                                        result.AddError(ErrorRechazo);
                                }
                            }
                            else
                            {
                                result.AddError(ErrorNoAprovada);
                            }
                        }
                        else
                        {
                            result.AddError(ErrorNoAprovada);
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

        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            return new RefundPaymentResult { Errors = new[] { "Refund method not supported" } };
        }

        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            var result = new List<string>();
            if (!form.TryGetValue("payerId", out StringValues payerId) || StringValues.IsNullOrEmpty(payerId))
                result.Add("PayerId no enviado");

            return result;
        }

        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            return new VoidPaymentResult { Errors = new[] { "Void method not supported" } };
        }
        #endregion
        #region Tools     

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
            var checkoutAttributesXml = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.CheckoutAttributes);
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
            //get price
            var paymentAdditionalFee = _paymentService.GetAdditionalHandlingFee(shoppingCart, PluginDescriptor.SystemName);
            var paymentPrice = _taxService.GetPaymentMethodAdditionalFee(paymentAdditionalFee, false, customer);

            if (paymentPrice <= decimal.Zero)
                return null;

            //create item
            return new Item
            {
                name = $"Payment method ({PluginDescriptor.FriendlyName}) additional fee",
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

        protected DetailsAmountInfo GetAmountDetails(ProcessPaymentRequest paymentRequest, IList<ShoppingCartItem> shoppingCart, IList<Item> items)
        {
            //get shipping total
            var shipping = _orderTotalCalculationService.GetShoppingCartShippingTotal(shoppingCart, false);
            var shippingTotal = shipping ?? 0;

            //get tax total
            var taxTotal = _orderTotalCalculationService.GetTaxTotal(shoppingCart, out SortedDictionary<decimal, decimal> _);

            //get subtotal
            decimal subTotal;
            if (items != null && items.Any())
            {
                //items passed to PayPal, so calculate subtotal based on them
                subTotal = items.Sum(item => !decimal.TryParse(item.price, out decimal tmpPrice) || !int.TryParse(item.quantity, out int tmpQuantity) ? 0 : tmpPrice * tmpQuantity);
            }
            else
                subTotal = paymentRequest.OrderTotal - shippingTotal - taxTotal;

            //adjust order total to avoid PayPal payment error: "Transaction amount details (subtotal, tax, shipping) must add up to specified amount total"
            paymentRequest.OrderTotal = Math.Round(shippingTotal, 2) + Math.Round(subTotal, 2) + Math.Round(taxTotal, 2);

            //create amount details
            return new DetailsAmountInfo
            {
                shipping = shippingTotal.ToString("N", new CultureInfo("en-US")),
                subtotal = subTotal.ToString("N", new CultureInfo("en-US")),
                tax = taxTotal.ToString("N", new CultureInfo("en-US"))
            };
        }

        private static async Task<string> PatchPaypalPaymentAsync(HttpClient http, AuthToken accessToken, string paymentId, string payerId, ProcessPaymentRequest processPaymentRequest, PayPalPlusPaymentSettings paypalPlusPaymentSettings, string urlnofications)
        {
            var method = new HttpMethod("PATCH");
            HttpRequestMessage request = new HttpRequestMessage(method, $"v1/payments/payment/{paymentId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Access_Token);

            List<Object> cambios = new List<Object>();
            Object invoice = new
            {
                op = "add",
                path = "/transactions/0/invoice_number",
                value = processPaymentRequest.OrderGuid.ToString()
            };
            cambios.Add(invoice);

            string contentr = string.Empty;
            var content = JsonConvert.SerializeObject(cambios);
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await http.SendAsync(request);
            if (Convert.ToInt16(response.StatusCode) == 200 || Convert.ToInt16(response.StatusCode) == 201)
                contentr = await response.Content.ReadAsStringAsync();
            else
                contentr = "400";

            return contentr;
        }

        private static async Task<PayPalPaymentExecutedResponse> ExecutePaypalPaymentAsync(HttpClient http, AuthToken accessToken, string paymentId, string payerId, ProcessPaymentRequest processPaymentRequest, PayPalPlusPaymentSettings paypalPlusPaymentSettings, string urlnofications, IList<ShoppingCartItem> cart, IOrderTotalCalculationService orderTotalCalculationService, DetailsAmountInfo amountInfo)
        {
            //get total discount amount
            var orderTotal = orderTotalCalculationService.GetShoppingCartTotal(cart,
                out decimal discountAmount,
                out List<DiscountForCaching> _, out List<AppliedGiftCard> _, out int _, out decimal _);

            if (discountAmount <= decimal.Zero)
                discountAmount = 0;

            orderTotalCalculationService.GetShoppingCartSubTotal(cart, false,
                out decimal subdiscountAmount,
                out List<DiscountForCaching> subdiscountforCath, out decimal subTotalSinDescuento, out decimal subtotalConDescuento, out SortedDictionary<decimal, decimal> taxRates);
            var taxTotal = orderTotalCalculationService.GetTaxTotal(cart, true);
            var shipping = orderTotalCalculationService.GetShoppingCartShippingTotal(cart, false);
            var shippingTotal = shipping ?? 0;

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"v1/payments/payment/{paymentId}/execute");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Access_Token);
            request.Headers.Add("PayPal-Partner-Attribution-Id", "TecnofinPPP_Cart");
            List<RestObjects.Transactions> transacciones = new List<RestObjects.Transactions>();
            RestObjects.Transactions transaccion = new RestObjects.Transactions()
            {
                amount = new RestObjects.AmountInfo
                {
                    currency = paypalPlusPaymentSettings.Currency,
                    details = amountInfo,
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
