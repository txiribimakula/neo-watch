using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;
using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;

namespace NeoWatch
{
    internal static class EditBlueprintsCommand
    {
        public const int CommandId = 252;
        private const string BlueprintsMoniker = "neoWatch.general.linkedListMemoryBlueprints";

        [Guid("E3684F31-344E-42EA-9047-B620FDC7AC25")]
        private sealed class UnifiedSettingsService { }

        public static void Initialize(NeoWatchPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var commands = package.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            commands?.AddCommand(new MenuCommand((sender, args) => Edit(package),
                new CommandID(NeoWatchCommand.CommandSet, CommandId)));
        }

        private static void Edit(NeoWatchPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var manager = package.GetService(typeof(UnifiedSettingsService)) as ISettingsManager;
                if (manager == null)
                {
                    var page = (BlueprintsOptionPage)package.GetDialogPage(typeof(BlueprintsOptionPage));
                    string original = page.LinkedListMemoryBlueprints;
                    new BlueprintEditorWindow(original, text =>
                    {
                        if (page.LinkedListMemoryBlueprints != original) return ChangedElsewhere;
                        page.SaveBlueprints(text);
                        return null;
                    }).ShowModal();
                    return;
                }

                var reader = manager.GetReader();
                string initial = reader.GetValueOrThrow<string>(BlueprintsMoniker);
                new BlueprintEditorWindow(initial, text =>
                {
                    // A separate settings tab may have changed while this editor was open.
                    if (reader.GetValueOrThrow<string>(BlueprintsMoniker) != initial) return ChangedElsewhere;
                    if (text == initial) return null;
                    var writer = manager.GetWriter("Neo Watch");
                    var change = writer.EnqueueChange(BlueprintsMoniker, text);
                    if (change.Outcome != SettingChangeOutcome.PendingCommit
                        && change.Outcome != SettingChangeOutcome.PendingCommitWithoutValidation)
                        return change.Message ?? "Visual Studio rejected the settings change.";
                    var commit = writer.RequestCommit("Update memory blueprints");
                    if (commit.Outcome == SettingCommitOutcome.PendingApproval)
                    {
                        (package.GetService(typeof(SVsStatusbar)) as IVsStatusbar)?.SetText(
                            "Blueprint changes are waiting for approval in Visual Studio settings.");
                        return null;
                    }
                    return commit.Outcome == SettingCommitOutcome.Success
                        || commit.Outcome == SettingCommitOutcome.NoChangesQueued
                        ? null : commit.Message ?? "Visual Studio could not save the blueprints.";
                }).ShowModal();
            }
            catch (Exception exception)
            {
                VsShellUtilities.ShowMessageBox(package, "Unable to open blueprints. " + exception.Message,
                    "Neo Watch", OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private const string ChangedElsewhere = "Blueprints changed in another window. Cancel and reopen before saving.";
    }
}
