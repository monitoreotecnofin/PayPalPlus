using Nop.Core.Domain.Payments;
using Nop.Services.Events;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.UI;
using System.Linq;

namespace Nop.Plugin.Payments.PayPalPlus.Services
{
    public class EventConsumer : IConsumer<PageRenderingEvent>
    {
        #region Fields
        private readonly ILocalizationService _localizationService;
        private readonly IPaymentService _paymentService;
        private readonly PaymentSettings _paymentSettings;
        private readonly IPaymentPluginManager _paymentPluginManager;
        #endregion
        #region Ctor

        public EventConsumer(ILocalizationService localizationService,
            IPaymentService paymentService,
            IPaymentPluginManager paymentPluginManager,
            PaymentSettings paymentSettings)
        {
            this._localizationService = localizationService;
            this._paymentService = paymentService;
            this._paymentPluginManager = paymentPluginManager;
            this._paymentSettings = paymentSettings;
        }
        #endregion

        #region Methods        
        
        public void HandleEvent(PageRenderingEvent eventMessage)
        {
            //check whether the plugin is active
            if (!_paymentPluginManager.IsPluginActive("Payments.PayPalPlus"))
                return;

            //add js script to one page checkout
            if (eventMessage.GetRouteNames().Any(routeName => routeName.Equals("CheckoutOnePage")))
                eventMessage.Helper?.AddScriptParts(ResourceLocation.Footer, "https://www.paypalobjects.com/webstatic/ppplusdcc/ppplusdcc.min.js", excludeFromBundle: true);          

        }

        #endregion
    }
}
