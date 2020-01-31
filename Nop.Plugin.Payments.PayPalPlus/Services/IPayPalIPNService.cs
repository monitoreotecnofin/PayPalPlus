namespace Nop.Plugin.Payments.PayPalPlus.Services
{
    public interface IPayPalIPNService
    {
        void HandleIPN(string ipnData);
    }
}
