using CasCap.Common.Configuration.Management.Controllers;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace CasCap.Common.Configuration.Management.Services;

internal sealed class RuntimeConfigurationManagementControllerConvention<T> : IApplicationModelConvention where T : class
{
    public void Apply(ApplicationModel application)
    {
        var controller = application.Controllers.SingleOrDefault(model =>
            model.ControllerType.AsType() == typeof(RuntimeConfigurationManagementController<T>));
        if (controller is null)
            return;
        // ApiExplorer does not infer the closed success DTO when controller-wide error
        // metadata accompanies generic HttpResults. Supply only that missing metadata.
        foreach (var action in controller.Actions)
        {
            var response = action.ActionMethod.Name switch
            {
                nameof(RuntimeConfigurationManagementController<T>.ReadAsync) => typeof(RuntimeConfigurationDocument<T>),
                nameof(RuntimeConfigurationManagementController<T>.Antiforgery) => typeof(RuntimeConfigurationAntiforgeryResponse),
                nameof(RuntimeConfigurationManagementController<T>.Preview) => typeof(PreviewRuntimeConfigurationResponse<T>),
                nameof(RuntimeConfigurationManagementController<T>.PublishAsync) => typeof(PublishRuntimeConfigurationResponse<T>),
                _ => null,
            };
            if (response is not null)
                action.Filters.Add(new ProducesResponseTypeAttribute(response, 200, "application/json"));
        }
    }
}
