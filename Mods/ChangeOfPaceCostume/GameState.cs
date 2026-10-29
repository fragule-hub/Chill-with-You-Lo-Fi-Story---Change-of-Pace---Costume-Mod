using System;

namespace ChangeOfPaceCostume;

/// <summary>
/// Read-only queries about the game's own state. Kept separate from the decoration
/// controller so hair logic and patch handlers do not depend on decoration internals.
/// </summary>
internal static class GameState
{
    /// <summary>
    /// True while the game treats main episode 32 (the finale) specially: unlocked but
    /// not finished yet. During this window the game suppresses all limited-time events,
    /// forces Normal hair and uses its own ending presentation, so the mod stays silent
    /// and lets the game default win. After the finale is read, normal behavior resumes.
    /// </summary>
    public static bool IsFinalePending()
    {
        try
        {
            var progress = Bulbul.SaveDataManager.Instance.ScenarioProgressData;
            return progress != null
                && progress.NextEpisodeNumber == 32f
                && progress.FinishReadMainEpisodeNumber < 32f;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
