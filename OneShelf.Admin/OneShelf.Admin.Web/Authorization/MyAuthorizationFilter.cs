using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using OneShelf.Admin.Web.Controllers;

namespace OneShelf.Admin.Web.Authorization
{
    public class MyAuthorizationFilterAttribute : Attribute, IAuthorizationFilter, IFilterFactory
    {
        private IConfiguration _configuration;

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var expected = _configuration.GetValue<string>(HomeController.AuthCookieName) ?? Guid.NewGuid().ToString();
            if (((ControllerActionDescriptor)context.ActionDescriptor).MethodInfo
                .GetCustomAttribute<AllowAnonymousAttribute>() == null
                && context.HttpContext.Request.Cookies[HomeController.AuthCookieName] != expected)
                context.Result = new RedirectToActionResult("Login", "Home", null);
        }

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            _configuration = serviceProvider.GetRequiredService<IConfiguration>();
            return this;
        }

        public bool IsReusable { get; }
    }
}
