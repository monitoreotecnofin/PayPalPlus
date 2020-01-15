using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.PayPalPlus
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {            
            //IPN
            routes.MapRoute("Plugin.Payments.PayPalPlus.IPNHandler",
                 "Plugins/PaymentPayPalPlus/IPNHandler",
                 new { controller = "PaymentPayPalPlus", action = "IPNHandler" },
                 new[] { "Nop.Plugin.Payments.PayPalPlus.Controllers" }
            );
            //Cancel
            routes.MapRoute("Plugin.Payments.PayPalPlus.CancelOrder",
                 "Plugins/PaymentPayPalPlus/CancelOrder",
                 new { controller = "PaymentPayPalPlus", action = "CancelOrder" },
                 new[] { "Nop.Plugin.Payments.PayPalPlus.Controllers" }
            );
            //Cancel
            routes.MapRoute("Plugin.Payments.PayPalPlus.AprovalOrder",
                 "Plugins/PaymentPayPalPlus/AprovalOrder",
                 new { controller = "PaymentPayPalPlus", action = "AprovalOrder" },
                 new[] { "Nop.Plugin.Payments.PayPalPlus.Controllers" }
            );
        }
        public int Priority
        {
            get
            {
                return 0;
            }
        }
    }
}
