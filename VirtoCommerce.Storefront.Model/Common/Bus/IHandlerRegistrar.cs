using System;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Storefront.Model.Common.Messages;

namespace VirtoCommerce.Storefront.Model.Common.Bus
{
    public interface IHandlerRegistrar
    {
        void RegisterHandler<T>(Func<T, CancellationToken, Task> handler) where T : class, IMessage;
    }
}
