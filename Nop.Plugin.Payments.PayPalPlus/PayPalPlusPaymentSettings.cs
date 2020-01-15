using Nop.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.PayPalPlus
{
    public class PayPalPlusPaymentSettings : ISettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether to use sandbox (testing environment)
        /// </summary>
        public bool UseSandbox { get; set; }

        public string Name { get; set; }

        public string ClientId { get; set; }

        public string SecretId { get; set; }

        public string EnviromentSandBox { get; set; }

        public string EnviromentLive { get; set; }

        public string Language { get; set; }

        public bool DisallowRememberedCards { get; set; }

        public string IFrameHeight { get; set; }

        /// <summary>
        /// Gets or sets an additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }
        //public string ScriptCheckOutOnePage { get; set; }
        //public string ScriptCheckOutPageToPage { get; set; }

        public string Currency { get; set; }
        public string CountryTwoLetters { get; set; }
    }
}
