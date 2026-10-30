using CasCap.Common.Configuration.Management.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CasCap.Common.Configuration.Management.Attributes;

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class RuntimeConfigurationManagementBodyAttribute : ModelBinderAttribute
{
    public RuntimeConfigurationManagementBodyAttribute() : base(typeof(RuntimeConfigurationManagementBodyBinder))
        => BindingSource = BindingSource.Body;
}
