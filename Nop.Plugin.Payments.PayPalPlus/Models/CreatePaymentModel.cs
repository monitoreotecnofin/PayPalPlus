using Nop.Web.Framework.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nop.Plugin.Payments.PayPalPlus.Models
{
    public class CreatePaymentModel : BaseNopModel
    {
        public string State { get; set; }

        public string Mensaje { get; set; }
        public string Exception { get; set; }
    }
}
