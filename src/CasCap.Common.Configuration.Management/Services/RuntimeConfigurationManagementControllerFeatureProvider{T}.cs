using CasCap.Common.Configuration.Management.Controllers;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Reflection;

namespace CasCap.Common.Configuration.Management.Services;

internal sealed class RuntimeConfigurationManagementControllerFeatureProvider<T> : IApplicationFeatureProvider<ControllerFeature> where T : class
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        => feature.Controllers.Add(typeof(RuntimeConfigurationManagementController<T>).GetTypeInfo());
}
