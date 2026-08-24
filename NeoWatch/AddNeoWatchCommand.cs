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

        private MenuCommand addFromWatchCommand;

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
            addFromWatchCommand = new MenuCommand(this.AddFromWatch, fromWatchCommandID);
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
            TextDocument textDocument = this.dte.ActiveDocument.Object() as TextDocument;
            string expressionName = GetExpressionNameAtCursor(textDocument.Selection);

            if (!string.IsNullOrEmpty(expressionName))
            {
                // temporarily disable adding through UI while adding programatically to avoid interferences.
                this.viewModel.CanUserAddRows = false;

                WatchItem watchItem = new Loading.WatchItem();
                this.viewModel.WatchItems.Add(watchItem);

                this.viewModel.CanUserAddRows = true;

                watchItem.Name = expressionName;
            }
        }

        /// <summary>
        /// Adds whatever is selected in the Watch, Locals or Autos window.
        ///
        /// Those windows do not expose their selection through any public API, so the only way in
        /// is to have Visual Studio copy the rows itself and read them back. The clipboard is put
        /// back as it was, because taking someone's clipboard for a menu click is rude.
        /// </summary>
        private void AddFromWatch(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IDataObject previousClipboard = TryGetClipboard();
            string copied;

            try
            {
                this.dte.ExecuteCommand("Edit.Copy");
                copied = System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : null;
            }
            catch (Exception)
            {
                // No selection, a window that will not copy, or a locked clipboard.
                return;
            }
            finally
            {
                RestoreClipboard(previousClipboard);
            }

            AddExpressions(WatchExpressionParser.Parse(copied));
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
