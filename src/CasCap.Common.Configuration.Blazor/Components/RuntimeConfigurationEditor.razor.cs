using CasCap.Common.Configuration.Blazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using System.Text.Json;

namespace CasCap.Common.Configuration.Blazor.Components;

/// <summary>Edits one host-selected browser-safe DTO through the authorized management API.</summary>
public sealed partial class RuntimeConfigurationEditor<T>(RuntimeConfigurationClient<T> runtimeConfigurationClient) : ComponentBase, IDisposable where T : class
{
    private readonly string _id = $"runtime-controls-{Guid.NewGuid():N}";
    private CancellationTokenSource _cancellation = new();
    private RuntimeConfigurationDestination? _destination;
    private RuntimeConfigurationDocument<T>? _document;
    private EditContext? _editContext;
    private ValidationMessageStore? _validation;
    private T? _value;
    private string _reason = "";
    private int? _lifetimeMinutes;
    private bool _confirmed;
    private bool _previewed;
    private string? _previewJson;
    private bool _busy;
    private bool _error;
    private string _message = "";
    private bool _reloadRequested;
    private string? _blockedNavigation;

    /// <summary>Gets or sets the fixed host-selected destination. This is not a permission grant.</summary>
    [Parameter, EditorRequired]
    public required RuntimeConfigurationDestination Destination { get; set; }

    /// <summary>Gets or sets typed input markup bound to the independent editable DTO.</summary>
    [Parameter, EditorRequired]
    public required RenderFragment<T> Fields { get; set; }

    /// <summary>Gets or sets the accessible editor heading.</summary>
    [Parameter]
    public string Title { get; set; } = "Runtime controls";

    /// <summary>Gets or sets the optional durable-commit notification; it is not an activation notification.</summary>
    [Parameter]
    public EventCallback<RuntimeConfigurationDocument<T>> OnCommitted { get; set; }

    /// <summary>Gets or sets the host navigation manager.</summary>
    [Inject]
    public required NavigationManager Navigation { get; set; }

    /// <inheritdoc/>
    protected override async Task OnParametersSetAsync()
    {
        ArgumentNullException.ThrowIfNull(Destination);
        ArgumentNullException.ThrowIfNull(Fields);
        if (_destination == Destination)
            return;
        await _cancellation.CancelAsync();
        _cancellation.Dispose();
        _cancellation = new();
        _destination = Destination;
        if (_editContext is not null)
            _editContext.OnFieldChanged -= FieldChanged;
        _document = null;
        _value = null;
        _editContext = null;
        _validation = null;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _busy = true;
        _reloadRequested = false;
        _blockedNavigation = null;
        _message = "";
        var cancellation = _cancellation.Token;
        try
        {
            var result = await runtimeConfigurationClient.ReadAsync(Destination, cancellation);
            if (cancellation.IsCancellationRequested)
                return;
            if (result.Succeeded)
            {
                _document = result.Value!;
                _value = _document.Value;
                SetContext();
                _reason = "";
                _lifetimeMinutes = null;
                _confirmed = false;
                _previewed = false;
                _error = false;
            }
            else
            {
                _document = null;
                _error = true;
                _message = result.Message;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (!cancellation.IsCancellationRequested)
                _busy = false;
        }
    }

    private void SetContext()
    {
        if (_editContext is not null)
            _editContext.OnFieldChanged -= FieldChanged;
        _editContext = new EditContext(_value!);
        _validation = new ValidationMessageStore(_editContext);
        _editContext.OnFieldChanged += FieldChanged;
    }

    private void FieldChanged(object? sender, FieldChangedEventArgs args)
    {
        if (!ReferenceEquals(args.FieldIdentifier.Model, this))
        {
            _previewed = false;
            _confirmed = false;
        }
        _validation?.Clear();
    }

    private async Task PreviewAsync()
    {
        if (_busy || _editContext is null || _value is null)
            return;
        _validation!.Clear();
        _previewed = false;
        if (!_editContext.Validate())
            return;
        _busy = true;
        var cancellation = _cancellation.Token;
        try
        {
            var result = await runtimeConfigurationClient.PreviewAsync(Destination, _value, cancellation);
            if (cancellation.IsCancellationRequested)
                return;
            _error = !result.Succeeded;
            _message = result.Succeeded ? "Validated. Review the destination, reason and lifetime before confirming publication." : result.Message;
            _previewed = result.Succeeded;
            _previewJson = result.Succeeded ? JsonSerializer.Serialize(_value) : null;
            AddServerErrors(result.Errors);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (!cancellation.IsCancellationRequested)
                _busy = false;
        }
    }

    private async Task PublishAsync()
    {
        if (_busy || _value is null || _editContext is null || _document is null)
            return;
        _validation!.Clear();
        if (!_previewed || _previewJson != JsonSerializer.Serialize(_value) || !_confirmed)
        {
            _error = true;
            _message = "Validate the current values and explicitly confirm publication first.";
            return;
        }
        if (string.IsNullOrWhiteSpace(_reason) || _reason.Length > 500)
            _validation.Add(new FieldIdentifier(this, nameof(_reason)), "Supply a publication reason of 1-500 characters.");
        if (_lifetimeMinutes is < 1 or > 1440)
            _validation.Add(new FieldIdentifier(this, nameof(_lifetimeMinutes)), "Lifetime must be between 1 and 1440 minutes.");
        if (!_editContext.Validate())
            return;
        _busy = true;
        var cancellation = _cancellation.Token;
        try
        {
            var result = await runtimeConfigurationClient.PublishAsync(Destination, new()
            {
                Value = _value,
                ExpectedRevision = _document.Revision,
                Reason = _reason,
                ExpiresAtUtc = _lifetimeMinutes is { } minutes ? DateTimeOffset.UtcNow.AddMinutes(minutes) : null,
            }, cancellation);
            if (cancellation.IsCancellationRequested)
                return;
            _error = !result.Succeeded;
            _message = result.Succeeded ? "Committed. Runtime activation is pending and is not confirmed by this editor." : result.Message;
            AddServerErrors(result.Errors);
            _previewed = false;
            _confirmed = false;
            if (result.Succeeded)
            {
                _document = result.Value!.Document;
                _value = _document.Value;
                SetContext();
                await OnCommitted.InvokeAsync(_document);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (!cancellation.IsCancellationRequested)
                _busy = false;
        }
    }

    private void AddServerErrors(IReadOnlyDictionary<string, string[]> errors)
    {
        foreach (var (field, messages) in errors)
        {
            var member = field.StartsWith("value.", StringComparison.OrdinalIgnoreCase) ? field[6..] : field;
            var property = typeof(T).GetProperties().FirstOrDefault(value => string.Equals(value.Name, member, StringComparison.OrdinalIgnoreCase));
            var identifier = property is not null ? new FieldIdentifier(_value!, property.Name)
                : new FieldIdentifier(_value!, string.Empty);
            _validation!.Add(identifier, messages);
        }
        _editContext!.NotifyValidationStateChanged();
    }

    private Task RequestReloadAsync()
    {
        if (_editContext?.IsModified() == true)
        {
            _reloadRequested = true;
            return Task.CompletedTask;
        }
        return LoadAsync();
    }

    private Task ReloadConfirmedAsync() => LoadAsync();

    private void KeepEditing()
    {
        _reloadRequested = false;
        _blockedNavigation = null;
    }

    private Task BeforeNavigationAsync(LocationChangingContext context)
    {
        if (_editContext?.IsModified() == true)
        {
            context.PreventNavigation();
            _blockedNavigation = context.TargetLocation;
        }
        return Task.CompletedTask;
    }

    private void LeavePage()
    {
        var target = _blockedNavigation;
        _blockedNavigation = null;
        _editContext?.MarkAsUnmodified();
        if (target is not null)
            Navigation.NavigateTo(target);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
        if (_editContext is not null)
            _editContext.OnFieldChanged -= FieldChanged;
    }
}
