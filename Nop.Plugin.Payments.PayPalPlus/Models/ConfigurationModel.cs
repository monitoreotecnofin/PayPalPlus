using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;
using System;
using System.Collections.Generic;


namespace Nop.Plugin.Payments.PayPalPlus.Models
{
    public class ConfigurationModel : BaseNopModel
    {

        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalPlus.Fields.Name")]
        public string Name { get; set; }
        public bool Name_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalPlus.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }
        public bool UseSandbox_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalPlus.Fields.ClientId")]
        public string ClientId { get; set; }
        public bool ClientId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalPlus.Fields.SecretId")]
        public string SecretId { get; set; }
        public bool SecretId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalPlus.Fields.EnviromentSandBox")]
        public string EnviromentSandBox { get; set; }
        public bool EnviromentSandBox_OverrideForStore { get; set; }
        [NopResourceDisplayName("Plugins.Payments.PayPalPlus.Fields.EnviromentLive")]
        public string EnviromentLive { get; set; }
        public bool EnviromentLive_OverrideForStore { get; set; }
        [NopResourceDisplayName("Plugins.Payments.PayPalPlus.Fields.Language")]
        public string Language { get; set; }
        public bool Language_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalPlus.Fields.DisallowRememberedCards")]
        public bool DisallowRememberedCards { get; set; }
        public bool DisallowRememberedCards_OverrideForStore { get; set; }


        [NopResourceDisplayName("Plugins.Payments.PayPalPlus.Fields.IFrameHeight")]
        public string IFrameHeight { get; set; }
        public bool IFrameHeight_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalPlus.Fields.AdditionalFee")]
        public decimal AdditionalFee { get; set; }
        public bool AdditionalFee_OverrideForStore { get; set; }


        [NopResourceDisplayName("Plugins.Payments.PayPalPlus.Fields.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }
        public bool AdditionalFeePercentage_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutOnePage")]
        public string ScriptCheckOutOnePage { get; set; }
        public bool ScriptCheckOutOnePage_OverrideForStore { get; set; }


        [NopResourceDisplayName("Plugins.Payments.PayPalPlus.Fields.ScriptCheckOutPageToPage")]
        public string ScriptCheckOutPageToPage { get; set; }
        public bool ScriptCheckOutPageToPage_OverrideForStore { get; set; }


        [NopResourceDisplayName("Plugins.Payments.PayPalPlus.Fields.Currency")]
        public string Currency { get; set; }
        public bool Currency_OverrideForStore { get; set; }


        [NopResourceDisplayName("Plugins.Payments.PayPalPlus.Fields.CountryTwoLetters")]
        public string CountryTwoLetters { get; set; }
        public bool CountryTwoLetters_OverrideForStore { get; set; }
    }

}
