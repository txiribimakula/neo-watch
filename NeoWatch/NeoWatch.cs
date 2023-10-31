using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NeoWatch.Views;
using System;
using System.Runtime.InteropServices;

namespace NeoWatch
{
    [Guid("6FD34CCE-6A7A-4016-878E-6A639BD79D69")]
    class NeoWatch : ToolWindowPane
    {
        DebuggerEvents DebuggerEvents;

        public NeoWatch() : base(null)
        {
            Caption = "Neo Watch";

            DTE DTE2 = NeoWatchCommand.Instance.ServiceProvider.GetService(typeof(DTE)) as DTE;

            if (DTE2 != null)
            {
                BlueprintsOptionPage page = (BlueprintsOptionPage)NeoWatchCommand.Instance.package.GetDialogPage(typeof(BlueprintsOptionPage));
                ViewModel viewModel = new ViewModel(DTE2.Debugger, page.Patterns, page.TypeKindPairs);
                page.OptionChangedEvent += viewModel.OnToolsOptionsBlueprintsChanged;

                NeoWatchWindow window = new NeoWatchWindow();
                window.DataContext = viewModel;

                Content = window;
                DebuggerEvents = DTE2.Events.DebuggerEvents;
                DebuggerEvents.OnEnterBreakMode += viewModel.OnEnterBreakMode;
                DebuggerEvents.OnEnterRunMode += NeoWatchCommand.Instance.RunHandler;
                DebuggerEvents.OnEnterDesignMode += NeoWatchCommand.Instance.DesignHandler;
            }
        }
    }
}
