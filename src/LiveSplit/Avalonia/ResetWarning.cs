using System.Linq;

using LiveSplit.Model;

namespace LiveSplit.Avalonia;

public static class ResetWarning
{
    public static bool ShouldAskToUpdateBestTimes(LiveSplitState state)
    {
        for (int index = 0; index < state.Run.Count; index++)
        {
            if (LiveSplitStateHelper.CheckBestSegment(state, index, state.CurrentTimingMethod))
            {
                return true;
            }
        }

        ISegment lastSegment = state.Run.Last();
        return (lastSegment.SplitTime[state.CurrentTimingMethod] != null
                && lastSegment.PersonalBestSplitTime[state.CurrentTimingMethod] == null)
            || lastSegment.SplitTime[state.CurrentTimingMethod] < lastSegment.PersonalBestSplitTime[state.CurrentTimingMethod];
    }
}
