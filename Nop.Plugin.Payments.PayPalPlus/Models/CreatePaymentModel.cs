using Nop.Web.Framework.Mvc;
using System;

namespace Nop.Plugin.Payments.PayPalPlus.Models
{
    public class CreatePaymentModel : BaseNopModel
    {
        public string State { get; set; }
        public string Mensaje { get; set; }
        public string Exception { get; set; }
    }
}
