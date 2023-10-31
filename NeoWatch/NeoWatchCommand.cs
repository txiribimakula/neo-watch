using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeoWatch
{
    internal sealed class NeoWatchCommand
    {
        public const int CommandId = 256;

        public static readonly Guid CommandSet = new Guid("AB6200EA-5C89-4F3C-AEEB-1374F1F578FB");

        public readonly NeoWatchPackage package;

        private MenuCommand menuCommand;

        private NeoWatchCommand(NeoWatchPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            menuCommand = new MenuCommand(this.ShowToolWindow, menuCommandID);
            commandService.AddCommand(menuCommand);
        }

        public static NeoWatchCommand Instance
        {
            get;
            private set;
        }

        public IServiceProvider ServiceProvider
        {
            get
            {
                return package;
            }
        }

        public static void Initialize(NeoWatchPackage package)
        {
            OleMenuCommandService commandService = package.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

            Instance = new NeoWatchCommand(package, commandService);
        }

        private void ShowToolWindow(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ToolWindowPane window = this.package.FindToolWindow(typeof(NeoWatch), 0, true);
            if ((null == window) || (null == window.Frame))
            {
                throw new NotSupportedException("Cannot create tool window");
            }

            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }

        public void RunHandler(dbgEventReason reason)
        {
            menuCommand.Visible = true;
        }

        public void DesignHandler(dbgEventReason reason)
        {
            menuCommand.Visible = false;
        }
    }
}
