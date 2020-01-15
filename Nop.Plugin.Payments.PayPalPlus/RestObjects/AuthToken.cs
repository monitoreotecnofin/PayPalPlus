using System;
using System.Collections.Generic;
using System.Text;

namespace Nop.Plugin.Payments.PayPalPlus.RestObjects
{
    public class AuthToken
    {
        public string Scope { get; set; }

        public string Nonce { get; set; }

        public string Access_Token { get; set; }

        public string Tokey_Type { get; set; }

        public string App_Id { get; set; }

        public string Expires { get; set; }
    }
}
