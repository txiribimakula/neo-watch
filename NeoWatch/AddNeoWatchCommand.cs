using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NeoWatch.Loading;
using System;
using System.ComponentModel.Design;
using System.Text.RegularExpressions;
using DTE = EnvDTE;

namespace NeoWatch
{
    internal sealed class AddNeoWatchCommand
    {
        public const int CommandId = 255;

        public static readonly Guid CommandSet = new Guid("AB6200EA-5C89-4F3C-AEEB-1374F1F578FB");

        public readonly NeoWatchPackage package;

        private MenuCommand menuCommand;

        private DTE::DTE dte;

        private ViewModel viewModel;

        private AddNeoWatchCommand(NeoWatchPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            menuCommand = new MenuCommand(this.Add, menuCommandID);
            commandService.AddCommand(menuCommand);
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
