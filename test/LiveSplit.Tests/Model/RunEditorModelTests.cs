using System;
using System.IO;
using System.Linq;

using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunSavers;

using Xunit;

namespace LiveSplit.Tests.Model;

/// <summary>
/// Pins the model-mutation contracts that the new tabbed RunEditorDialog relies on:
///   • Custom variables round-trip through XMLRunSaver when marked permanent.
///   • Adding a custom comparison name leaves every segment with a default Time entry.
///   • Removing an attempt by index drops it from AttemptHistory and from every
///     segment's SegmentHistory simultaneously.
///   • PersonalBestSplitTime is the same slot as Comparisons["Personal Best"].
///   • <see cref="IRun.GameIconPng"/> survives an XMLRunSaver round-trip via the new
///     &lt;GameIconPng&gt; element.
/// </summary>
public class RunEditorModelTests
{
    private static Run NewRun(int segmentCount = 3)
    {
        var run = new Run(new StandardComparisonGeneratorsFactory());
        for (int i = 0; i < segmentCount; i++)
        {
            run.AddSegment($"Segment {i + 1}");
        }

        return run;
    }

    [Fact]
    public void CustomVariable_PermanentRoundTripsThroughSaver()
    {
        Run run = NewRun();
        run.Metadata.CustomVariables["seed"] = new CustomVariable("12345", isPermanent: true);

        using var ms = new MemoryStream();
        new XMLRunSaver().Save(run, ms);
        string xml = System.Text.Encoding.UTF8.GetString(ms.ToArray());

        Assert.Contains("<Variable name=\"seed\">12345</Variable>", xml);
    }

    [Fact]
    public void CustomVariable_NonPermanentIsOmitted()
    {
        Run run = NewRun();
        run.Metadata.CustomVariables["transient"] = new CustomVariable("zzz", isPermanent: false);

        using var ms = new MemoryStream();
        new XMLRunSaver().Save(run, ms);
        string xml = System.Text.Encoding.UTF8.GetString(ms.ToArray());

        Assert.DoesNotContain("transient", xml);
    }

    [Fact]
    public void AddCustomComparison_SeedsEverySegment()
    {
        Run run = NewRun(segmentCount: 4);

        run.CustomComparisons.Add("Bingo");
        foreach (ISegment segment in run)
        {
            segment.Comparisons["Bingo"] = new Time();
        }

        Assert.All(run, seg =>
            Assert.True(seg.Comparisons.ContainsKey("Bingo"), "Bingo column missing on segment"));
    }

    [Fact]
    public void RemoveAttempt_DropsFromAttemptHistoryAndSegmentHistory()
    {
        Run run = NewRun();
        var attempt = new Attempt(7, new Time(TimeSpan.FromSeconds(60), null), null, null, null);
        run.AttemptHistory.Add(attempt);
        run[0].SegmentHistory.Add(7, new Time(TimeSpan.FromSeconds(20), null));
        run[1].SegmentHistory.Add(7, new Time(TimeSpan.FromSeconds(40), null));

        // Editor's RemoveSelectedAttempt path: drop from history + walk every segment.
        run.AttemptHistory.Remove(run.AttemptHistory.First(a => a.Index == 7));
        foreach (ISegment seg in run)
        {
            seg.SegmentHistory.Remove(7);
        }

        Assert.DoesNotContain(run.AttemptHistory, a => a.Index == 7);
        Assert.All(run, seg => Assert.False(seg.SegmentHistory.ContainsKey(7)));
    }

    [Fact]
    public void PersonalBestSplitTime_IsSameSlotAsComparisonsPersonalBest()
    {
        Run run = NewRun();
        var t = new Time(TimeSpan.FromSeconds(123), null);
        run[0].Comparisons[Run.PersonalBestComparisonName] = t;

        Assert.Equal(t.RealTime, run[0].PersonalBestSplitTime.RealTime);
    }

    [Fact]
    public void GameIconPng_RoundTripsThroughSaver()
    {
        Run run = NewRun();
        // Tiny but valid PNG signature + IHDR — content doesn't matter for the round-trip.
        byte[] pngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0xDE, 0xAD, 0xBE, 0xEF };
        run.GameIconPng = pngBytes;

        using var ms = new MemoryStream();
        new XMLRunSaver().Save(run, ms);

        var doc = new System.Xml.XmlDocument();
        ms.Position = 0;
        doc.Load(ms);
        System.Xml.XmlNodeList nodes = doc.GetElementsByTagName("GameIconPng");

        Assert.Single(nodes);
        byte[] decoded = Convert.FromBase64String(nodes[0].InnerText);
        Assert.Equal(pngBytes, decoded);
    }

    [Fact]
    public void Run_Clone_DeepClonesGameIconPng()
    {
        Run run = NewRun();
        run.GameIconPng = new byte[] { 1, 2, 3 };

        Run clone = run.Clone();
        clone.GameIconPng[0] = 99;

        Assert.Equal(1, run.GameIconPng[0]);
    }
}
