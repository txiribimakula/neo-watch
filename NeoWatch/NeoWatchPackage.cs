using Microsoft.VisualStudio.Shell;
using NeoWatch.Loading;
using NeoWatch.Views;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Task = System.Threading.Tasks.Task;

namespace NeoWatch
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks> [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuidString)]
    [ProvideToolWindow(typeof(NeoWatch))]
    [ProvideBindingPath]
    [ProvideOptionPage(typeof(BlueprintsOptionPage), "Neo Watch", "Blueprints", 0, 0, true)]
    public sealed class NeoWatchPackage : AsyncPackage
    {
        /// <summary>
        /// NeoWatchPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "2f2f2923-9433-4dcb-b3b6-373c61e85461";

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            NeoWatchCommand.Initialize(this);
        }

        public new object GetService(Type serviceType)
        {
            return base.GetService(serviceType);
        }

        public new DialogPage GetDialogPage(Type dialogPageType)
        {
            return base.GetDialogPage(dialogPageType);
        }

        #endregion
    }

    [Guid("E77FB104-A860-4B35-A46D-BE33E3616FD4")]
    public class BlueprintsOptionPage : DialogPage
    {
        private string blueprints;
        public string Blueprints
        {
            get { return blueprints; }
            set { blueprints = value; OnOptionChanged(); }
        }


        // TODO: set this from configuration window.
        public Dictionary<PatternKind, string> Patterns { get; set; } = new Dictionary<PatternKind, string>()
            {
                { PatternKind.Type, @"(?<type>\w+): (?<parse>.*)" },
                { PatternKind.Segment, @"(?<initialPoint>.*) - (?<finalPoint>.*)" },
                { PatternKind.Arc, @"C: (?<centerPoint>.*) R: (?<radius>.*) AngIni: (?<initialAngle>.*) AngPaso: (?<sweepAngle>.*)" },
                { PatternKind.Circle, @"C: (?<centerPoint>.*) R: (?<radius>.*)" },
                { PatternKind.Point, @"^\((?<x>\d*\.?\d+),(?<y>\d*\.?\d+)\)$" }
            };

        // TODO: set this from configuration window.
        public Dictionary<string, PatternKind> TypeKindPairs { get; set; } = new Dictionary<string, PatternKind>()
            {
                { "Pnt", PatternKind.Point },
                { "Seg", PatternKind.Segment },
                { "Arc", PatternKind.Arc },
                { "Cir", PatternKind.Circle }
            };

        protected override IWin32Window Window
        {
            get
            {
                ToolsOptionsBlueprintsUserControl page = new ToolsOptionsBlueprintsUserControl();
                page.BlueprintsOptionPage = this;
                page.Initialize();
                return page;
            }
        }

        public event OptionChanged OptionChangedEvent;
        private void OnOptionChanged()
        {
            OptionChangedEvent?.Invoke(Patterns, TypeKindPairs);
        }
        public delegate void OptionChanged(Dictionary<PatternKind, string> patterns, Dictionary<string, PatternKind> typeKindPairs);
    }
}
