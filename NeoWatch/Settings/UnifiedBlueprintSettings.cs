using Microsoft.VisualStudio.Utilities.UnifiedSettings;
using System.Runtime.InteropServices;

namespace NeoWatch.Settings
{
    /// <summary>
    /// Reads and writes the blueprints kept in Unified Settings. The text setting is the store:
    /// it keeps the registered default and the migration from the old options page, while the
    /// settings page shows the container types through <see cref="BlueprintExternalSettings"/>.
    /// </summary>
    internal static class UnifiedBlueprintSettings
    {
        public const string TextMoniker = "neoWatch.blueprints.linkedListMemoryBlueprints";

        [Guid("E3684F31-344E-42EA-9047-B620FDC7AC25")]
        internal sealed class Service
        {
        }

        public static string Read(ISettingsReader reader)
        {
            return reader.GetValueOrThrow<string>(TextMoniker);
        }

        /// <summary>Returns null once the change is on its way, or why it was rejected.</summary>
        public static string Write(ISettingsWriter writer, string text)
        {
            SettingChangeResult change = writer.EnqueueChange(TextMoniker, text);
            if (change.Outcome != SettingChangeOutcome.PendingCommit
                && change.Outcome != SettingChangeOutcome.PendingCommitWithoutValidation)
                return change.Message ?? "Visual Studio rejected the settings change.";

            SettingCommitResult commit = writer.RequestCommit("Update blueprints");
            if (commit.Outcome == SettingCommitOutcome.PendingApproval) return null;
            return commit.Outcome == SettingCommitOutcome.Success
                || commit.Outcome == SettingCommitOutcome.NoChangesQueued
                ? null : commit.Message ?? "Visual Studio could not save the blueprints.";
        }
    }
}
