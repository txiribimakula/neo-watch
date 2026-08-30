using Microsoft.VisualStudio.PlatformUI;
using NeoWatch.Settings;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace NeoWatch
{
    public partial class BlueprintEditorWindow : DialogWindow
    {
        private readonly BlueprintEditorModel model;
        private readonly Func<string, string> save;
        private BlueprintEntry pendingFocus;

        public BlueprintEditorWindow(string text, Func<string, string> save = null)
        {
            InitializeComponent();
            model = new BlueprintEditorModel(text);
            DataContext = model;
            this.save = save;
        }

        public string BlueprintText { get; private set; }

        private void AddBlueprint(object sender, RoutedEventArgs e)
        {
            pendingFocus = model.Add();
            ErrorText.Text = string.Empty;
            Dispatcher.BeginInvoke(new Action(() => ListScroll.ScrollToEnd()), DispatcherPriority.Loaded);
        }

        private void EditorLoaded(object sender, RoutedEventArgs e)
        {
            var editor = (TextBox)sender;
            if (editor.DataContext != pendingFocus) return;
            pendingFocus = null;
            editor.Focus();
        }

        private void RemoveBlueprint(object sender, RoutedEventArgs e)
        {
            var entry = (BlueprintEntry)((FrameworkElement)sender).DataContext;
            model.Entries.Remove(entry);
            ErrorText.Text = string.Empty;
        }

        private void CopyPrompt(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var stream = typeof(BlueprintEditorWindow).Assembly
                    .GetManifestResourceStream("NeoWatch.Resources.BlueprintPrompt.txt"))
                using (var reader = new StreamReader(stream))
                    System.Windows.Forms.Clipboard.SetDataObject(reader.ReadToEnd(), true, 5, 50);
                ErrorText.Text = string.Empty;
            }
            catch (ExternalException)
            {
                ErrorText.Text = "The clipboard is busy. Please try again.";
            }
        }

        private void SaveBlueprints(object sender, RoutedEventArgs e)
        {
            string text;
            string error;
            if (!model.TrySerialize(out text, out error))
            {
                ErrorText.Text = error;
                return;
            }
            try
            {
                error = save?.Invoke(text);
                if (error != null)
                {
                    ErrorText.Text = error;
                    return;
                }
                BlueprintText = text;
                DialogResult = true;
            }
            catch (Exception exception)
            {
                ErrorText.Text = "Unable to save blueprints. " + exception.Message;
            }
        }
    }
}
