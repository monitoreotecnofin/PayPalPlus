using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.PayPalPlus
{
    public partial class RouteProvider : IRouteProvider
    {
        /// <summary>
        /// Register routes
        /// </summary>
        /// <param name="routeBuilder">Route builder</param>
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {

            //IPN
            routeBuilder.MapRoute("Plugin.Payments.PayPalPlus.IPNHandler", "Plugins/PaymentPayPalPlus/IPNHandler",
                 new { controller = "PaymentPayPalPlus", action = "IPNHandler" });

            //Cancel
            routeBuilder.MapRoute("Plugin.Payments.PayPalPlus.CancelOrder", "Plugins/PaymentPayPalPlus/CancelOrder",
                 new { controller = "PaymentPayPalPlus", action = "CancelOrder" });

            //Approval
            routeBuilder.MapRoute("Plugin.Payments.PayPalPlus.AprovalOrder", "Plugins/PaymentPayPalPlus/AprovalOrder",
                 new { controller = "PaymentPayPalPlus", action = "AprovalOrder" });

        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority
        {
            get { return -1; }
        }
    }
}
