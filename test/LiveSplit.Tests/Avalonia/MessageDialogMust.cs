using System.Linq;

using LiveSplit.Avalonia.Dialogs;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class MessageDialogMust
{
    [Theory]
    [InlineData(MessageDialog.Buttons.OkCancel, new[] { "OK", "Cancel" })]
    [InlineData(MessageDialog.Buttons.YesNo, new[] { "Yes", "No" })]
    [InlineData(MessageDialog.Buttons.YesNoCancel, new[] { "Yes", "No", "Cancel" })]
    [InlineData(MessageDialog.Buttons.RetryCancel, new[] { "Retry", "Cancel" })]
    public void UseMasterButtonOrder(MessageDialog.Buttons buttons, string[] expected)
    {
        Assert.Equal(expected, MessageDialog.GetButtonSpecs(buttons).Select(x => x.Text));
    }

    [Theory]
    [InlineData(MessageDialog.Buttons.Ok, MessageResult.Ok)]
    [InlineData(MessageDialog.Buttons.OkCancel, MessageResult.Cancel)]
    [InlineData(MessageDialog.Buttons.YesNo, MessageResult.No)]
    [InlineData(MessageDialog.Buttons.YesNoCancel, MessageResult.Cancel)]
    [InlineData(MessageDialog.Buttons.RetryCancel, MessageResult.Cancel)]
    public void ResolveXAndEscapeToMasterCancelResult(MessageDialog.Buttons buttons, MessageResult expected)
    {
        Assert.Equal(expected, MessageDialog.GetCancelResult(buttons));
    }

    [Theory]
    [InlineData(MessageDialog.Buttons.Ok, MessageResult.Ok)]
    [InlineData(MessageDialog.Buttons.OkCancel, MessageResult.Ok)]
    [InlineData(MessageDialog.Buttons.YesNo, MessageResult.Yes)]
    [InlineData(MessageDialog.Buttons.YesNoCancel, MessageResult.Yes)]
    [InlineData(MessageDialog.Buttons.RetryCancel, MessageResult.Ok)]
    public void ResolveEnterToMasterDefaultResult(MessageDialog.Buttons buttons, MessageResult expected)
    {
        Assert.Equal(expected, MessageDialog.GetDefaultResult(buttons));
    }
}
