using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.PayPalPlus.Services
{
    public interface IPayPalIPNService
    {
        void HandleIPN(string ipnData);
    }
}
