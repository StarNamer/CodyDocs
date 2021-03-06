﻿using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Task = System.Threading.Tasks.Task;

namespace CodyDocs
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class DocumentCodeSpanCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 256;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("e9659912-2099-4744-8c8b-cd264069ca84");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        private DTE2 _applicationObject { get; set; }
        private IVsTextManager2 _textManager { get; set; }


        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentCodeSpanCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private DocumentCodeSpanCommand(AsyncPackage package, OleMenuCommandService commandService, DTE2 appObj, IVsTextManager2 txtMngr)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            this._applicationObject = appObj ?? throw new ArgumentNullException(nameof(_applicationObject));
            this._textManager = txtMngr ?? throw new ArgumentNullException(nameof(_textManager));


            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static DocumentCodeSpanCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in DocumentCodeSpanCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync((typeof(IMenuCommandService))) as OleMenuCommandService;
            var applicationObject = await package.GetServiceAsync(typeof(DTE)) as DTE2;
            var textManager = await package.GetServiceAsync(typeof(SVsTextManager)) as IVsTextManager2;
            Instance = new DocumentCodeSpanCommand(package, commandService, applicationObject, textManager);
        }

        struct TextViewSelection
        {
            public TextViewPosition StartPosition { get; set; }
            public TextViewPosition EndPosition { get; set; }
            public string Text { get; set; }

            public TextViewSelection(TextViewPosition a, TextViewPosition b, string text)
            {
                StartPosition = TextViewPosition.Min(a, b);
                EndPosition = TextViewPosition.Max(a, b);
                Text = text;
            }
        }


        public struct TextViewPosition
        {
            private readonly int _column;
            private readonly int _line;

            public TextViewPosition(int line, int column)
            {
                _line = line;
                _column = column;
            }

            public int Line { get { return _line; } }
            public int Column { get { return _column; } }


            public static bool operator <(TextViewPosition a, TextViewPosition b)
            {
                if (a.Line < b.Line)
                {
                    return true;
                }
                else if (a.Line == b.Line)
                {
                    return a.Column < b.Column;
                }
                else
                {
                    return false;
                }
            }

            public static bool operator >(TextViewPosition a, TextViewPosition b)
            {
                if (a.Line > b.Line)
                {
                    return true;
                }
                else if (a.Line == b.Line)
                {
                    return a.Column > b.Column;
                }
                else
                {
                    return false;
                }
            }

            public static TextViewPosition Min(TextViewPosition a, TextViewPosition b)
            {
                return a > b ? b : a;
            }

            public static TextViewPosition Max(TextViewPosition a, TextViewPosition b)
            {
                return a > b ? a : b;
            }
        }

        private TextViewSelection GetSelection()
        {
            IVsTextView view;
            int result = _textManager.GetActiveView2(1, null, (uint)_VIEWFRAMETYPE.vftCodeWindow, out view);

            view.GetSelection(out int startLine, out int startColumn, out int endLine, out int endColumn);//end could be before beginning
            var start = new TextViewPosition(startLine, startColumn);
            var end = new TextViewPosition(endLine, endColumn);

            view.GetSelectedText(out string selectedText);

            TextViewSelection selection = new TextViewSelection(start, end, selectedText);
            return selection;
        }

        private string GetActiveDocumentFilePath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return _applicationObject.ActiveDocument.FullName;
        }

        private void ShowAddDocumentationWindow(string documentPath, TextViewSelection seletion)
        {
            var documentationControl = new AddDocumentationWindow();
            documentationControl.ShowDialog();
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            TextViewSelection selection = GetSelection();
            string activeDocumentPath = GetActiveDocumentFilePath();
            ShowAddDocumentationWindow(activeDocumentPath, selection);
        }
    }
}
