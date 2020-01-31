using Autofac;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Plugin.Payments.PayPalPlus.Services;

namespace Nop.Plugin.Payments.PayPalPlus
{
    public class DependencyRegistrar : IDependencyRegistrar
    {

        public void Register(ContainerBuilder builder, ITypeFinder typeFinder, NopConfig config)
        {
            builder.RegisterType<PayPalIPNService>().As<IPayPalIPNService>();
        }

        public int Order { get { return 99; } }
    }
}
