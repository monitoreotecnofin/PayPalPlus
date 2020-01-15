using Microsoft.AspNetCore.Mvc.Controllers;
using Nop.Core.Domain.Payments;
using Nop.Services.Events;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.UI;

namespace Nop.Plugin.Payments.PayPalPlus.Services
{
    public class EventConsumer :
       IConsumer<PageRenderingEvent>
    {
        #region Fields
        private readonly ILocalizationService _localizationService;
        private readonly IPaymentService _paymentService;        
        private readonly PaymentSettings _paymentSettings;
        #endregion
        #region Ctor

        public EventConsumer(ILocalizationService localizationService,
            IPaymentService paymentService,            
            PaymentSettings paymentSettings)
        {
            this._localizationService = localizationService;
            this._paymentService = paymentService;            
            this._paymentSettings = paymentSettings;
        }
        #endregion

        #region Methods        
        public void HandleEvent(PageRenderingEvent eventMessage)
        {
            {
                if (eventMessage?.Helper?.ViewContext?.ActionDescriptor == null)
                    return;

                //check whether the plugin is installed and is active
                var paypalplusPaymentMethod = _paymentService.LoadPaymentMethodBySystemName(PayPalPlusPaymentDefaults.SystemName);
                if (!(paypalplusPaymentMethod?.PluginDescriptor?.Installed ?? false) || !paypalplusPaymentMethod.IsPaymentMethodActive(_paymentSettings))
                    return;

                //add js sсript to one page checkout
                if (eventMessage.Helper.ViewContext.ActionDescriptor is ControllerActionDescriptor actionDescriptor &&
                    actionDescriptor.ControllerName == "Checkout" && actionDescriptor.ActionName == "OnePageCheckout" )
                {
                    eventMessage.Helper.AddScriptParts(ResourceLocation.Head, "https://www.paypalobjects.com/webstatic/ppplusdcc/ppplusdcc.min.js", excludeFromBundle: true);
                }
            }
        }
        #endregion
    }
}
