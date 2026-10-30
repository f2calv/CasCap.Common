namespace CasCap.Common.Configuration.Blazor.Tests.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class RuntimeConfigurationEditorTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public void TypedForm_PreviewsAndCommitsWithoutLosingIntegralRevision()
    {
        var editor = Render();
        editor.WaitForAssertion(() => Assert.Contains("9007199254740993", editor.Markup));
        editor.Find("#verbosity").Change(4);
        editor.Find("#feature").Change(true);
        editor.Find("#day").Change(nameof(DayOfWeek.Friday));
        editor.Find("#duration").Change("00:12:34");
        editor.Find("textarea").Change("Reviewed diagnostic adjustment");
        editor.Find("form").Submit();
        editor.WaitForAssertion(() => Assert.Contains("Validated.", editor.Markup));
        Assert.Equal(4, _handler.Previewed!.Value.Verbosity);
        Assert.True(_handler.Previewed.Value.FeatureEnabled);
        Assert.Equal(DayOfWeek.Friday, _handler.Previewed.Value.Day);
        Assert.Equal(new TimeSpan(0, 12, 34), _handler.Previewed.Value.Duration);
        Assert.True(Button(editor, "Publish").HasAttribute("disabled"));
        editor.Find("input[id$='-confirm']").Change(true);
        Button(editor, "Publish").Click();
        editor.WaitForAssertion(() => Assert.Contains("Committed. Runtime activation is pending", editor.Markup));
        Assert.Equal(1, _handler.Publications);
        Assert.Equal(9007199254740993, _handler.Published!.ExpectedRevision);
        Assert.Equal("Reviewed diagnostic adjustment", _handler.Published.Reason);
        Assert.Equal(4, _handler.Published.Value.Verbosity);
        Assert.Contains("9007199254740994", editor.Markup);
        Assert.False(editor.FindComponent<Microsoft.AspNetCore.Components.Forms.EditForm>().Instance.EditContext!.IsModified());
    }

    [Theory]
    [InlineData("verbosity", "9")]
    [InlineData("duration", "not-a-duration")]
    public void InvalidTypedInput_DoesNotReachPreview(string field, string input)
    {
        var editor = Render();
        editor.WaitForAssertion(() => Assert.NotEmpty(editor.FindAll("form")));
        editor.Find($"#{field}").Change(input);
        editor.Find("form").Submit();
        Assert.Equal(0, _handler.Previews);
        Assert.NotEmpty(editor.FindAll(".validation-message, .validation-errors"));
    }

    [Fact]
    public void ChangingPreviewedValues_RequiresNewPreviewAndConfirmation()
    {
        var editor = Render();
        editor.WaitForAssertion(() => Assert.NotEmpty(editor.FindAll("form")));
        editor.Find("form").Submit();
        editor.WaitForAssertion(() => Assert.Contains("Validated.", editor.Markup));
        editor.Find("input[id$='-confirm']").Change(true);
        Assert.False(Button(editor, "Publish").HasAttribute("disabled"));
        editor.Find("#feature").Change(true);
        Assert.True(Button(editor, "Publish").HasAttribute("disabled"));
        Assert.Equal(0, _handler.Publications);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Validation failed")]
    [InlineData(HttpStatusCode.Unauthorized, "Sign in")]
    [InlineData(HttpStatusCode.Forbidden, "permission")]
    [InlineData(HttpStatusCode.NotFound, "No document")]
    [InlineData(HttpStatusCode.Conflict, "document changed")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "uncertain")]
    public void ReadFailures_AreVisibleAndNeverRenderAnEditableFallback(HttpStatusCode status, string message)
    {
        _handler.ReadStatus = status;
        var editor = Render();
        editor.WaitForAssertion(() => Assert.Contains(message, editor.Find("[role='alert']").TextContent));
        Assert.Empty(editor.FindAll("form"));
        Assert.Equal(0, _handler.Publications);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Validation failed")]
    [InlineData(HttpStatusCode.Conflict, "document changed")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "uncertain")]
    public void PublicationFailure_PreservesEditsAndDoesNotRetry(HttpStatusCode status, string message)
    {
        _handler.PublishStatus = status;
        var editor = Render();
        editor.WaitForAssertion(() => Assert.NotEmpty(editor.FindAll("form")));
        editor.Find("#verbosity").Change(4);
        editor.Find("textarea").Change("Reason to retain");
        editor.Find("form").Submit();
        editor.WaitForAssertion(() => Assert.Contains("Validated.", editor.Markup));
        editor.Find("input[id$='-confirm']").Change(true);
        Button(editor, "Publish").Click();
        editor.WaitForAssertion(() => Assert.Contains(message, editor.Find("[role='alert']").TextContent));
        Assert.Equal("4", editor.Find("#verbosity").GetAttribute("value"));
        Assert.Equal("Reason to retain", editor.Find("textarea").GetAttribute("value"));
        Assert.Equal(1, _handler.Publications);
        Assert.True(Button(editor, "Publish").HasAttribute("disabled"));
        if (status == HttpStatusCode.BadRequest)
            Assert.Contains("The chosen verbosity is not allowed.", editor.Markup);
    }

    [Fact]
    public void Reload_RequiresExplicitDiscardOfDirtyValues()
    {
        var editor = Render();
        editor.WaitForAssertion(() => Assert.NotEmpty(editor.FindAll("form")));
        editor.Find("#verbosity").Change(4);
        Button(editor, "Reload current").Click();
        Assert.Equal(1, _handler.Reads);
        Assert.Contains("discards your unsaved edits", editor.Markup);
        Button(editor, "Keep editing").Click();
        Assert.Equal("4", editor.Find("#verbosity").GetAttribute("value"));
        Button(editor, "Reload current").Click();
        Button(editor, "Discard edits and reload").Click();
        editor.WaitForAssertion(() => Assert.Equal(2, _handler.Reads));
        Assert.Equal("2", editor.Find("#verbosity").GetAttribute("value"));
    }

    [Fact]
    public void Form_LabelsAndNativeKeyboardActionsArePresent()
    {
        var editor = Render();
        editor.WaitForAssertion(() => Assert.NotEmpty(editor.FindAll("form")));
        foreach (var input in editor.FindAll("input, textarea, select"))
        {
            var id = input.Id;
            Assert.False(string.IsNullOrWhiteSpace(id));
            Assert.Single(editor.FindAll($"label[for='{id}']"));
        }
        Assert.Equal("submit", Button(editor, "Validate / preview").GetAttribute("type"));
        Assert.Equal("button", Button(editor, "Publish").GetAttribute("type"));
        Assert.Contains("sample-app / staging / controls", editor.Markup);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PendingRead_CancelsOnDestinationChangeOrDisposal(bool changeDestination)
    {
        _handler.PendingRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var editor = Render();
        editor.WaitForAssertion(() => Assert.Contains("Working...", editor.Markup));
        if (changeDestination)
        {
            _handler.PendingRead = null;
            editor.Render(parameters => parameters.Add(value => value.Destination, new("sample-app", "other", "controls")));
            editor.WaitForAssertion(() => Assert.Contains("sample-app / other / controls", editor.Markup));
        }
        else
            await _context.DisposeComponentsAsync();
        Assert.True(await _handler.ReadCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Equal(0, _handler.Publications);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("Reviewed reason", 0)]
    [InlineData("Reviewed reason", 1441)]
    public void InvalidPublicationMetadata_DoesNotSendMutation(string reason, int? lifetime)
    {
        var editor = Render();
        editor.WaitForAssertion(() => Assert.NotEmpty(editor.FindAll("form")));
        editor.Find("textarea").Change(reason);
        if (lifetime is not null)
            editor.Find("input[id$='-lifetime']").Change(lifetime.Value);
        editor.Find("form").Submit();
        editor.WaitForAssertion(() => Assert.Contains("Validated.", editor.Markup));
        editor.Find("input[id$='-confirm']").Change(true);
        Button(editor, "Publish").Click();
        Assert.Equal(0, _handler.Publications);
        Assert.NotEmpty(editor.FindAll(".validation-errors"));
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<EditorHarness> editor, string text) =>
        editor.FindAll("button").Single(button => button.TextContent == text);
}
