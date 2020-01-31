using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.PayPalPlus.Models
{
    public class PaymentInfoModel : BaseNopModel
    {
        //public string PayerId { get; set; }
        //public string PayerEmail { get; set; }
        //public string PayerPhone { get; set; }
        //public string PayerFirstName { get; set; }
        //public string PayerLastName { get; set; }

        //public string Country { get; set; }
        //public string AccessToken { get; set; }
        //public string Mode { get; set; }
        public string Respuesta { get; set; }
        //public string UrlTienda { get; set; }
        //public string UrlTiendaAproval { get; set; }

        public string Scriptppp { get; set; }
        public bool Error { get; set; }

        public bool OnePageCheckoutEnabled { get; set; }
    }
}
