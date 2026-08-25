using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NeoWatch.Loading;
using System;
using System.ComponentModel.Design;
using System.Windows;
using System.Text.RegularExpressions;
using DTE = EnvDTE;

namespace NeoWatch
{
    internal sealed class AddNeoWatchCommand
    {
        public const int CommandId = 255;

        /// <summary>Same command, reached from the Watch, Locals and Autos context menus.</summary>
        public const int AddFromWatchCommandId = 254;

        public static readonly Guid CommandSet = new Guid("AB6200EA-5C89-4F3C-AEEB-1374F1F578FB");

        public readonly NeoWatchPackage package;

        private MenuCommand menuCommand;

        private OleMenuCommand addFromWatchCommand;

        private DTE::DTE dte;

        private ViewModel viewModel;

        private AddNeoWatchCommand(NeoWatchPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            menuCommand = new MenuCommand(this.Add, menuCommandID);
            commandService.AddCommand(menuCommand);

            var fromWatchCommandID = new CommandID(CommandSet, AddFromWatchCommandId);
            var fromWatch = new OleMenuCommand(this.AddFromWatch, fromWatchCommandID);
            // The only hook that runs while the menu is being built, which is the only moment the
            // floating variable view is still the command target. See CaptureDataTipExpression.
            fromWatch.BeforeQueryStatus += this.CaptureDataTipExpression;
            addFromWatchCommand = fromWatch;
            commandService.AddCommand(addFromWatchCommand);
        }

        public static AddNeoWatchCommand Instance
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

            Instance = new AddNeoWatchCommand(package, commandService);
        }

        public void SetDte(DTE::DTE dte, ViewModel viewModel)
        {
            this.dte = dte;
            this.viewModel = viewModel;
        }

        private void Add(object sender, EventArgs e)
        {
            AddExpressions(ExpressionsFromEditor());
        }

        /// <summary>The expression at the caret, which is what the code window command reads.</summary>
        private System.Collections.Generic.List<string> ExpressionsFromEditor()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var expressions = new System.Collections.Generic.List<string>();

            try
            {
                TextDocument textDocument = this.dte.ActiveDocument.Object() as TextDocument;
                if (textDocument == null) return expressions;

                string expressionName = GetExpressionNameAtCursor(textDocument.Selection);
                if (!string.IsNullOrEmpty(expressionName))
                {
                    expressions.Add(expressionName);
                }
            }
            catch (Exception)
            {
                // No document, or one that will not give up a text selection.
            }

            return expressions;
        }

        /// <summary>
        /// Adds whatever the invoking surface is pointing at.
        ///
        /// Two surfaces reach here and they are not alike. The Watch, Locals and Autos windows do
        /// not expose their selection through any public API, so the only way in is to have Visual
        /// Studio copy the rows itself and read them back.
        ///
        /// The floating variable view is a different story: it closes the moment this command is
        /// invoked, so by the time we run there is nothing left to copy from — neither Copy
        /// Expression nor a plain copy has a target any more. What remains underneath is the
        /// editor, which is exactly what the code window command already reads.
        /// </summary>
        private void AddFromWatch(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (IsToolWindowActive())
            {
                AddExpressions(ExpressionsFromSelectedRows());
                return;
            }

            var expressions = new System.Collections.Generic.List<string>();
            if (capturedExpression != null)
            {
                expressions.Add(capturedExpression);
            }
            else
            {
                // The menu was not the floating view, or the capture came back empty.
                expressions = ExpressionsFromEditor();
            }

            capturedExpression = null;
            AddExpressions(expressions);
        }

        // Copy Expression, from the debugger command set. Raised by GUID and id: the canonical
        // name is undocumented, and getting it wrong fails silently.
        private const string DebugCommandSet = "{C9DD4A59-47FB-11D2-83E7-00C04F9902C1}";
        private const int CmdIdCopyExpression = 0x149;

        private string capturedExpression;

        /// <summary>
        /// Grabs the expression the floating variable view is showing, while it is still there.
        ///
        /// That view closes as soon as a menu item is clicked, so by the time the click handler
        /// runs there is nothing left to ask. Building the menu, though, happens while it is alive
        /// and still the active command target — which is why its own Add Watch knows what you
        /// right-clicked and ours could not.
        ///
        /// Only for that surface: the Watch, Locals and Autos windows are still there at click
        /// time and read their selection then.
        /// </summary>
        private void CaptureDataTipExpression(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            capturedExpression = null;
            if (IsToolWindowActive()) return;

            IDataObject previousClipboard = TryGetClipboard();
            try
            {
                string copied = TryCopy(() =>
                {
                    object customIn = null;
                    object customOut = null;
                    this.dte.Commands.Raise(DebugCommandSet, CmdIdCopyExpression, ref customIn, ref customOut);
                });

                System.Collections.Generic.List<string> parsed = WatchExpressionParser.Parse(copied);
                if (parsed.Count > 0)
                {
                    capturedExpression = parsed[0];
                }
            }
            finally
            {
                RestoreClipboard(previousClipboard);
            }
        }

        /// <summary>Copies the selected rows and reads them back, putting the clipboard back after.</summary>
        private System.Collections.Generic.List<string> ExpressionsFromSelectedRows()
        {
            IDataObject previousClipboard = TryGetClipboard();
            string copied;

            try
            {
                copied = TryCopy(() => this.dte.ExecuteCommand("Edit.Copy"));
            }
            finally
            {
                // Taking someone's clipboard for a menu click is rude.
                RestoreClipboard(previousClipboard);
            }

            return WatchExpressionParser.Parse(copied);
        }

        private static string TryCopy(Action copy)
        {
            try
            {
                // Cleared first so a command that runs but copies nothing cannot be mistaken for
                // one that worked.
                System.Windows.Clipboard.Clear();
                copy();
            }
            catch (Exception)
            {
                // Not offered on this surface, or nothing is selected.
                return null;
            }

            try
            {
                if (!System.Windows.Clipboard.ContainsText()) return null;

                string text = System.Windows.Clipboard.GetText();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                return null;
            }
        }

        private bool IsToolWindowActive()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                DTE::Window active = this.dte.ActiveWindow;
                // Watch, Locals and Autos are tool windows; the editor is a document.
                return active != null && active.Kind != "Document";
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Creates a watch item per expression, newest last.</summary>
        private void AddExpressions(System.Collections.Generic.List<string> expressions)
        {
            if (expressions.Count == 0) return;

            // Adding through the grid at the same time interferes with adding programmatically.
            this.viewModel.CanUserAddRows = false;

            var added = new System.Collections.Generic.List<WatchItem>(expressions.Count);
            foreach (string expression in expressions)
            {
                var watchItem = new WatchItem();
                this.viewModel.WatchItems.Add(watchItem);
                added.Add(watchItem);
            }

            this.viewModel.CanUserAddRows = true;

            // Names last: setting one starts its load, and the collection has wired up its handler.
            for (int i = 0; i < added.Count; i++)
            {
                added[i].Name = expressions[i];
            }
        }

        private static IDataObject TryGetClipboard()
        {
            try
            {
                return System.Windows.Clipboard.GetDataObject();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void RestoreClipboard(IDataObject previous)
        {
            try
            {
                if (previous == null)
                {
                    System.Windows.Clipboard.Clear();
                }
                else
                {
                    System.Windows.Clipboard.SetDataObject(previous, true);
                }
            }
            catch (Exception)
            {
                // Losing the previous clipboard is a nuisance, not a reason to fail the command.
            }
        }

        private string GetExpressionNameAtCursor(EnvDTE.TextSelection textSelection)
        {
            string expressionAtCursor = textSelection.Text;

            if (!string.IsNullOrEmpty(expressionAtCursor))
            {
                return expressionAtCursor;
            }

            int originalLine = textSelection.CurrentLine;
            int originalColumn = textSelection.CurrentColumn;

            expressionAtCursor += GetPartialExpressionNameAtLeftOfCursor(textSelection);

            textSelection.MoveToDisplayColumn(originalLine, originalColumn);

            expressionAtCursor += GetPartialExpressionNameAtRightOfCursor(textSelection);

            textSelection.MoveToDisplayColumn(originalLine, originalColumn);

            return expressionAtCursor;
        }

        private string GetPartialExpressionNameAtLeftOfCursor(EnvDTE.TextSelection textSelection)
        {
            string expressionAtLeftOfCursor = string.Empty;
            string previousTextSelection;
            do
            {
                textSelection.WordLeft(true);
                previousTextSelection = expressionAtLeftOfCursor;
                expressionAtLeftOfCursor = textSelection.Text;
            }
            while (previousTextSelection != expressionAtLeftOfCursor && !Regex.IsMatch(expressionAtLeftOfCursor, @"[^\w\[\]\.]"));

            return Regex.Replace(expressionAtLeftOfCursor, @".*?[^\w\[\]\.]+", string.Empty);
        }
        private string GetPartialExpressionNameAtRightOfCursor(EnvDTE.TextSelection textSelection)
        {
            string expressionAtRightOfCursor = string.Empty;
            string previousTextSelection;
            do
            {
                textSelection.WordRight(true);
                previousTextSelection = expressionAtRightOfCursor;
                expressionAtRightOfCursor = textSelection.Text;
            }
            while (previousTextSelection != expressionAtRightOfCursor && !Regex.IsMatch(expressionAtRightOfCursor, @"[^\w\[\]]"));

            return Regex.Replace(expressionAtRightOfCursor, @"[^\w\[\]].*?", string.Empty);
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
