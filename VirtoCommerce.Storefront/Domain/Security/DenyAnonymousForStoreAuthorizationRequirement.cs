using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Storefront.Model;

namespace VirtoCommerce.Storefront.Domain.Security
{
    public class DenyAnonymousForStoreAuthorizationRequirement : IAuthorizationRequirement
    {
        public const string PolicyName = "DenyAnonymousForStore";
    }

    public class DenyAnonymousForStoreAuthorizationHandler : AuthorizationHandler<DenyAnonymousForStoreAuthorizationRequirement>
    {
        private readonly IWorkContextAccessor _workContextAccessor;

        public DenyAnonymousForStoreAuthorizationHandler(IWorkContextAccessor workContextAccessor)
        {
            _workContextAccessor = workContextAccessor;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, DenyAnonymousForStoreAuthorizationRequirement requirement)
        {
            var workContext = _workContextAccessor.WorkContext;

            if (workContext.CurrentUser.IsRegisteredUser || workContext.CurrentStore.AnonymousUsersAllowed)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
