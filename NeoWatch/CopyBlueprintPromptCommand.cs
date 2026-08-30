using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NeoWatch
{
    internal static class CopyBlueprintPromptCommand
    {
        public const int CommandId = 253;

        public static void Initialize(NeoWatchPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var commands = package.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            commands?.AddCommand(new MenuCommand((sender, args) => Copy(package),
                new CommandID(NeoWatchCommand.CommandSet, CommandId)));
        }

        private static void Copy(NeoWatchPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            using (var stream = typeof(CopyBlueprintPromptCommand).Assembly
                .GetManifestResourceStream("NeoWatch.Resources.BlueprintPrompt.txt"))
            using (var reader = new StreamReader(stream))
            {
                try
                {
                    Clipboard.SetDataObject(reader.ReadToEnd(), true, 5, 50);
                    var statusBar = package.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
                    statusBar?.SetText("Blueprint prompt copied. Add your C++ declarations before sending it to your AI.");
                }
                catch (ExternalException)
                {
                    VsShellUtilities.ShowMessageBox(package,
                        "The clipboard is busy. Please try copying the prompt again.", "Neo Watch",
                        OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
            }
        }
    }
}
