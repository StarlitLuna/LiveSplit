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
}
