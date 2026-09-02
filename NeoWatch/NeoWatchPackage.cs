using Microsoft.VisualStudio.Shell;
using NeoWatch.Loading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Runtime.InteropServices;
using System.Threading;
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
    [ProvideSettingsManifest]
    [ProvideOptionPage(typeof(BlueprintsOptionPage), "Neo Watch", "General", 0, 0, true,
        IsInUnifiedSettings = true)]
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
            AddNeoWatchCommand.Initialize(this);
            CopyBlueprintPromptCommand.Initialize(this);
            EditBlueprintsCommand.Initialize(this);
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
        public const string DefaultLinkedListMemoryBlueprints =
@"# One section per native container type.
[DemoSegmentLinkedList]
Count=Count
Head=Head
Next=Next
Tag=demoSegment.type|Int32
Line.Tag=0
Line.InitialX=demoSegment.segment.line.demoInitialPoint.demoX|Float64
Line.InitialY=demoSegment.segment.line.demoInitialPoint.demoY|Float64
Line.FinalX=demoSegment.segment.line.demoFinalPoint.demoX|Float64
Line.FinalY=demoSegment.segment.line.demoFinalPoint.demoY|Float64
Arc.Tag=1
Arc.CenterX=demoSegment.segment.arc.demoCenterPoint.demoX|Float64
Arc.CenterY=demoSegment.segment.arc.demoCenterPoint.demoY|Float64
Arc.Radius=demoSegment.segment.arc.demoRadius|Float64
Arc.InitialAngle=demoSegment.segment.arc.demoInitialAngle|Float64
Arc.SweepAngle=demoSegment.segment.arc.demoSweepAngle|Float64

[DemoPointLinkedList]
Count=Count
Head=Head
Next=Next
Point.X=x|Float32
Point.Y=y|Float32

# MSVC std::vector<DemoPoint>, including f10Points and stressPoints.
[std::vector<DemoPoint,std::allocator<DemoPoint>>]
Storage=Contiguous
Head=_Mypair._Myval2._Myfirst
End=_Mypair._Myval2._Mylast
Capacity=_Mypair._Myval2._Myend
Point.X=demoX|Float64
Point.Y=demoY|Float64

# MSVC node storage: one point per vector element, without following Next.
[std::vector<DemoListOfItself,std::allocator<DemoListOfItself>>]
Storage=Contiguous
Head=_Mypair._Myval2._Myfirst
End=_Mypair._Myval2._Mylast
Capacity=_Mypair._Myval2._Myend
Point.X=x|Float32
Point.Y=y|Float32

# MSVC std::vector<DemoLineSegment>, including stressSegments.
[std::vector<DemoLineSegment,std::allocator<DemoLineSegment>>]
Storage=Contiguous
Head=_Mypair._Myval2._Myfirst
End=_Mypair._Myval2._Mylast
Capacity=_Mypair._Myval2._Myend
Line.InitialX=demoInitialPoint.demoX|Float64
Line.InitialY=demoInitialPoint.demoY|Float64
Line.FinalX=demoFinalPoint.demoX|Float64
Line.FinalY=demoFinalPoint.demoY|Float64

# MSVC std::vector<DemoArcSegment>, including stressArcs.
[std::vector<DemoArcSegment,std::allocator<DemoArcSegment>>]
Storage=Contiguous
Head=_Mypair._Myval2._Myfirst
End=_Mypair._Myval2._Mylast
Capacity=_Mypair._Myval2._Myend
Arc.CenterX=demoCenterPoint.demoX|Float64
Arc.CenterY=demoCenterPoint.demoY|Float64
Arc.Radius=demoRadius|Float64
Arc.InitialAngle=demoInitialAngle|Float64
Arc.SweepAngle=demoSweepAngle|Float64";

        [Category("Experimental memory loader")]
        [DisplayName("Enabled")]
        [Description("Loads configured native linked lists and contiguous containers directly from process memory. Falls back to NatVis on any failure.")]
        [DefaultValue(false)]
        public bool EnableLinkedListMemoryLoader { get; set; }

        [Category("Experimental canvas")]
        [DisplayName("Enable GPU canvas")]
        [Description("Uses a persistent Direct3D canvas. Falls back to WPF if the device or geometry is unsupported.")]
        [DefaultValue(false)]
        public bool EnableGpuCanvas { get; set; }

        [Category("Experimental memory loader")]
        [DisplayName("Blueprints")]
        [Description("INI blueprints. Member values use path|Float32, Float64, Int32, UInt32, Int64 or UInt64.")]
        [DefaultValue(DefaultLinkedListMemoryBlueprints)]
        [Editor(typeof(Settings.BlueprintsPropertyEditor), typeof(UITypeEditor))]
        public string LinkedListMemoryBlueprints { get; set; } = DefaultLinkedListMemoryBlueprints;

        public event EventHandler MemoryLoaderSettingsChanged;

        internal void SaveBlueprints(string text)
        {
            string previous = LinkedListMemoryBlueprints;
            LinkedListMemoryBlueprints = text;
            try { SaveSettingsToStorage(); }
            catch
            {
                LinkedListMemoryBlueprints = previous;
                throw;
            }
            MemoryLoaderSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            base.OnApply(e);
            MemoryLoaderSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        [Browsable(false)]
        public Dictionary<PatternKind, string[]> Patterns { get; set; } = new Dictionary<PatternKind, string[]>()
            {
                { PatternKind.Type, new string[] { @"(?<type>\w+): (?<parse>.*)" } },
                { PatternKind.Segment, new string[] { @"(?<initialPoint>.*) - (?<finalPoint>.*)" } },
                { PatternKind.Arc, new string[] { @"C: (?<centerPoint>.*) R: (?<radius>.*) AngIni: (?<initialAngle>.*) AngPaso: (?<sweepAngle>.*)" } },
                { PatternKind.Circle, new string[] { @"C: (?<centerPoint>.*) R: (?<radius>.*)" } },
                // TODO: possibility to raise warning if the second pattern had to be used.
                { PatternKind.Point, new string[] { @"^\((?<x>\d*\.?\d+),(?<y>\d*\.?\d+)\)$", @"\((?<x>.*),(?<y>.*)\)" } }
            };

        [Browsable(false)]
        public Dictionary<string, PatternKind> TypeKindPairs { get; set; } = new Dictionary<string, PatternKind>()
            {
                { "Pnt", PatternKind.Point },
                { "Seg", PatternKind.Segment },
                { "Arc", PatternKind.Arc },
                { "Cir", PatternKind.Circle }
            };

    }
}
