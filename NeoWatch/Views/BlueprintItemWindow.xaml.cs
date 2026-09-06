using Microsoft.VisualStudio.PlatformUI;
using NeoWatch.Loading;
using NeoWatch.Settings;
using System;
using System.Collections.Generic;
using System.Windows;

namespace NeoWatch
{
    /// <summary>
    /// Edits one blueprint. The settings grid opens this from the Edit command on its row, so the
    /// dialog never shows the other blueprints.
    /// </summary>
    public partial class BlueprintItemWindow : DialogWindow
    {
        private readonly Func<string, string, string> save;

        public BlueprintItemWindow(string type, string definition, Func<string, string, string> save)
        {
            InitializeComponent();
            TypeBox.Text = type ?? string.Empty;
            DefinitionBox.Text = definition ?? string.Empty;
            this.save = save;
            Loaded += (sender, args) =>
            {
                if (TypeBox.Text.Length == 0) TypeBox.Focus();
                else DefinitionBox.Focus();
            };
        }

        private void Save(object sender, RoutedEventArgs e)
        {
            string type = TypeBox.Text.Trim();
            if (type.Length == 0)
            {
                Fail("Enter the container type as the debugger shows it.");
                return;
            }
            if (type.StartsWith("[", StringComparison.Ordinal) || type.Contains("]"))
            {
                Fail("Write the type on its own, without the surrounding brackets.");
                return;
            }

            string text = BlueprintSettingsList.ToIni(new[]
            {
                new KeyValuePair<string, string>(type, DefinitionBox.Text)
            });
            if (LinkedListMemoryBlueprintParser.Parse(text).Count != 1)
            {
                Fail("Incomplete or invalid blueprint: check the storage members and the geometry fields.");
                return;
            }

            try
            {
                string error = save == null ? null : save(type, DefinitionBox.Text);
                if (error != null)
                {
                    Fail(error);
                    return;
                }
                DialogResult = true;
            }
            catch (Exception exception)
            {
                Fail("Unable to save the blueprint. " + exception.Message);
            }
        }

        private void Fail(string message)
        {
            ErrorText.Text = message;
        }
    }
}
