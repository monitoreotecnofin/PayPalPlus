using System;
using System.Collections.Generic;
using System.Text;

namespace Nop.Plugin.Payments.PayPalPlus.RestObjects
{
    public class ExperienceProfileInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public CPresentation Presentation;
        public CInput_Fields Input_fields { get; set; }
        public bool Temporary { get; set; }
        public ExperienceProfileInfo()
        {
            Presentation = new CPresentation();
        }
        public ExperienceProfileInfo(string proFileName, string merchantName)
        {
            Presentation = new CPresentation();
            Name = proFileName;
            Presentation.Brand_Name = merchantName;
        }

    }

    public class CPresentation
    {
        public string Brand_Name { get; set; }
    }

    public class CInput_Fields
    {
        public int No_Shipping { get; set; }
        public int Address_Override { get; set; }
    }
}
