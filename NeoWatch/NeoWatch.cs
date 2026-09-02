using DTE = EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;
using System;
using System.Runtime.InteropServices;
using NeoWatch.Debugging;
using NeoWatch.Loading;

namespace NeoWatch
{
    [Guid("6FD34CCE-6A7A-4016-878E-6A639BD79D69")]
    class NeoWatch : ToolWindowPane
    {
        private const string MemoryLoaderEnabledMoniker = "neoWatch.general.enableLinkedListMemoryLoader";
        private const string MemoryLoaderBlueprintsMoniker = "neoWatch.general.linkedListMemoryBlueprints";
        private const string GpuCanvasMoniker = "neoWatch.general.enableGpuCanvas";

        [Guid("E3684F31-344E-42EA-9047-B620FDC7AC25")]
        private sealed class UnifiedSettingsService
        {
        }

        DTE::DebuggerEvents DebuggerEvents;
        private BlueprintsOptionPage optionsPage;
        private ViewModel viewModel;
        private ISettingsReader unifiedSettingsReader;
        private IDisposable unifiedSettingsSubscription;

        public NeoWatch() : base(null)
        {
            Caption = "Neo Watch";

            DTE::DTE DTE2 = NeoWatchCommand.Instance.ServiceProvider.GetService(typeof(DTE::DTE)) as DTE::DTE;

            if (DTE2 != null)
            {
                optionsPage = (BlueprintsOptionPage)NeoWatchCommand.Instance.package.GetDialogPage(typeof(BlueprintsOptionPage));
                var debugger = new Debugger(DTE2.Debugger);
                viewModel = new ViewModel(debugger, optionsPage.Patterns, optionsPage.TypeKindPairs, new DkmMemoryReader(debugger));
                optionsPage.MemoryLoaderSettingsChanged += OnMemoryLoaderSettingsChanged;
                InitializeMemoryLoaderOptions();

                NeoWatchWindow window = new NeoWatchWindow();
                window.DataContext = viewModel;

                AddNeoWatchCommand.Instance.SetDte(DTE2, viewModel);
                
                Content = window;
                DebuggerEvents = DTE2.Events.DebuggerEvents;
                DebuggerEvents.OnEnterBreakMode += viewModel.OnEnterBreakMode;
                DebuggerEvents.OnEnterRunMode += NeoWatchCommand.Instance.RunHandler;
                DebuggerEvents.OnEnterDesignMode += NeoWatchCommand.Instance.DesignHandler;
                DebuggerEvents.OnEnterDesignMode += viewModel.OnEnterDesignMode;
            }
        }

        private void OnMemoryLoaderSettingsChanged(object sender, EventArgs e)
        {
            if (unifiedSettingsReader == null)
            {
                ApplyMemoryLoaderOptions();
            }
        }

        private void InitializeMemoryLoaderOptions()
        {
            try
            {
                var settingsManager = NeoWatchCommand.Instance.package.GetService(typeof(UnifiedSettingsService))
                    as ISettingsManager;
                if (settingsManager != null)
                {
                    unifiedSettingsReader = settingsManager.GetReader();
                    unifiedSettingsSubscription = unifiedSettingsReader.SubscribeToChanges(
                        OnUnifiedSettingsChanged,
                        new[] { MemoryLoaderEnabledMoniker, MemoryLoaderBlueprintsMoniker, GpuCanvasMoniker });
                    ApplyUnifiedMemoryLoaderOptions();
                    return;
                }
            }
            catch (Exception exception)
            {
                ActivityLog.LogWarning("Neo Watch",
                    "Unified Settings are unavailable; using the legacy options store. " + exception.Message);
                unifiedSettingsSubscription?.Dispose();
                unifiedSettingsSubscription = null;
                unifiedSettingsReader = null;
            }

            ApplyMemoryLoaderOptions();
        }

        private void OnUnifiedSettingsChanged(SettingsUpdate update)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ApplyUnifiedMemoryLoaderOptions();
            });
        }

        private void ApplyUnifiedMemoryLoaderOptions()
        {
            if (viewModel == null || unifiedSettingsReader == null) return;

            try
            {
                bool enabled = unifiedSettingsReader.GetValueOrThrow<bool>(MemoryLoaderEnabledMoniker);
                string blueprints = unifiedSettingsReader.GetValueOrThrow<string>(MemoryLoaderBlueprintsMoniker);
                viewModel.ConfigureLinkedListMemoryLoading(enabled, blueprints);
                viewModel.ConfigureGpuCanvas(unifiedSettingsReader.GetValueOrThrow<bool>(GpuCanvasMoniker));
            }
            catch (Exception exception)
            {
                ActivityLog.LogWarning("Neo Watch",
                    "Unable to read Unified Settings; using the legacy options store. " + exception.Message);
                ApplyMemoryLoaderOptions();
            }
        }

        private void ApplyMemoryLoaderOptions()
        {
            if (viewModel == null || optionsPage == null) return;
            viewModel.ConfigureLinkedListMemoryLoading(optionsPage.EnableLinkedListMemoryLoader,
                optionsPage.LinkedListMemoryBlueprints);
            viewModel.ConfigureGpuCanvas(optionsPage.EnableGpuCanvas);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                unifiedSettingsSubscription?.Dispose();
                if (optionsPage != null)
                {
                    optionsPage.MemoryLoaderSettingsChanged -= OnMemoryLoaderSettingsChanged;
                }
            }
            base.Dispose(disposing);
        }
    }
}
