using System;

using LiveSplit.Avalonia.Dialogs;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class SpeedrunComSubmitDialogMust
{
    [Fact]
    public void OmitManualSubmissionDateWhenPersonalBestAttemptHasDate()
    {
        DateTime? date = SpeedrunComSubmitDialog.ResolveSubmissionDate(
            hasPersonalBestDateTime: true,
            selectedDate: new DateTimeOffset(2024, 3, 2, 9, 30, 0, TimeSpan.FromHours(-5)));

        Assert.Null(date);
    }

    [Fact]
    public void ConvertPickerDateToUtcMidnightSubmissionDate()
    {
        DateTime? date = SpeedrunComSubmitDialog.ResolveSubmissionDate(
            hasPersonalBestDateTime: false,
            selectedDate: new DateTimeOffset(2024, 3, 2, 9, 30, 0, TimeSpan.FromHours(-5)));

        Assert.Equal(new DateTime(2024, 3, 2, 0, 0, 0, DateTimeKind.Utc), date);
    }

    [Fact]
    public void RequireASelectedDateWhenManualDateIsNeeded()
    {
        DateTime? date = SpeedrunComSubmitDialog.ResolveSubmissionDate(
            hasPersonalBestDateTime: false,
            selectedDate: null);

        Assert.Null(date);
    }
}
