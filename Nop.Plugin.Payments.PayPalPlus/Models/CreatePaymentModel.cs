using Nop.Web.Framework.Mvc.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.PayPalPlus.Models
{
    public class CreatePaymentModel: BaseNopModel
    {
        public string State { get; set; }

        public string Mensaje { get; set; }
        public string Exception { get; set; }
    }
}
