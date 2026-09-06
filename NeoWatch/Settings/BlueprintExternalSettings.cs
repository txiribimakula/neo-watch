using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NeoWatch.Settings
{
    /// <summary>
    /// Serves the blueprint list shown in Tools &gt; Options. The list is an external setting so
    /// that each row can carry an Edit command of ours: the settings grid cannot show or edit a
    /// multi-line blueprint by itself. The values still live in the text setting, which keeps its
    /// registered default and its migration from the old options page.
    /// </summary>
    [Guid(ServiceGuidString)]
    public sealed class BlueprintExternalSettings : IExternalSettingsProvider, IExternalArrayItemCommandsProvider
    {
        public const string ServiceGuidString = "9E9B1C0E-4C4A-4E7C-9E5E-1D2C0B7A5F31";
        public const string ListMoniker = "blueprints.list";

        private readonly NeoWatchPackage package;
        private string listMoniker = ListMoniker;

        public BlueprintExternalSettings(NeoWatchPackage package)
        {
            this.package = package;
        }

        public event EventHandler<ExternalSettingsChangedEventArgs> SettingValuesChanged;
#pragma warning disable 67 // Required by the interface; this provider has no enums or messages.
        public event EventHandler<EnumSettingChoicesChangedEventArgs> EnumSettingChoicesChanged;
        public event EventHandler<DynamicMessageTextChangedEventArgs> DynamicMessageTextChanged;
        public event EventHandler ErrorConditionResolved;
#pragma warning restore 67

        public Task<ExternalSettingOperationResult<T>> GetValueAsync<T>(string moniker, CancellationToken token)
        {
            if (!IsList(moniker)) return Unknown<T>(moniker);
            try
            {
                // Answer change notifications with the moniker Visual Studio actually uses.
                listMoniker = moniker;
                return ExternalSettingOperationResult.ConvertSuccessResultTask<T>(Rows());
            }
            catch (Exception exception)
            {
                return ExternalSettingOperationResult.FailureResultTask<T>(
                    "Unable to read the blueprints. " + exception.Message,
                    ExternalSettingsErrorScope.SingleSettingOnly, false);
            }
        }

        public Task<ExternalSettingOperationResult> SetValueAsync<T>(string moniker, T value,
            CancellationToken token)
        {
            if (!IsList(moniker)) return Rejected("Neo Watch does not own the setting " + moniker + ".");
            try
            {
                // The grid only adds and removes rows; a new row starts empty and an existing one
                // keeps the blueprint it already had.
                var existing = BlueprintSettingsList.FromIni(ReadText());
                var kept = new List<KeyValuePair<string, string>>();
                foreach (string type in TypesOf(value))
                {
                    var match = existing.FirstOrDefault(item =>
                        string.Equals(item.Key, type, StringComparison.OrdinalIgnoreCase));
                    kept.Add(new KeyValuePair<string, string>(type, match.Key == null ? string.Empty : match.Value));
                }

                string error = SaveText(BlueprintSettingsList.ToIni(kept));
                return error == null ? ExternalSettingOperationResult.SuccessResultTask() : Rejected(error);
            }
            catch (Exception exception)
            {
                ActivityLog.LogWarning("Neo Watch", "Unable to save the blueprint list. " + exception.Message);
                return Rejected("Unable to save the blueprints. " + exception.Message);
            }
        }

        public Task<IReadOnlyList<IArrayItemCommand>> GetArrayItemCommandsAsync(string moniker,
            CancellationToken token)
        {
            IReadOnlyList<IArrayItemCommand> commands = IsList(moniker)
                ? new IArrayItemCommand[] { new EditBlueprintCommand(this) }
                : new IArrayItemCommand[0];
            return Task.FromResult(commands);
        }

        public Task<string> GetMessageTextAsync(string messageId, CancellationToken token)
        {
            return Task.FromResult(string.Empty);
        }

        public Task<ExternalSettingOperationResult<IReadOnlyList<EnumChoice>>> GetEnumChoicesAsync(
            string moniker, CancellationToken token)
        {
            return Unknown<IReadOnlyList<EnumChoice>>(moniker);
        }

        public Task OpenBackingStoreAsync(CancellationToken token)
        {
            // The blueprints live in the Visual Studio settings store; there is nothing to open.
            return Task.CompletedTask;
        }

        /// <summary>Opens one blueprint in its own dialog. A null type starts a new one.</summary>
        internal void Edit(string type)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var blueprints = BlueprintSettingsList.FromIni(ReadText());
                int index = type == null ? -1 : blueprints.FindIndex(item =>
                    string.Equals(item.Key, type, StringComparison.OrdinalIgnoreCase));
                var edited = index < 0
                    ? new KeyValuePair<string, string>(type ?? string.Empty, string.Empty)
                    : blueprints[index];

                var window = new BlueprintItemWindow(edited.Key, edited.Value, (newType, definition) =>
                {
                    var saved = new List<KeyValuePair<string, string>>(blueprints);
                    var entry = new KeyValuePair<string, string>(newType, definition);
                    if (index < 0) saved.Add(entry); else saved[index] = entry;

                    int duplicate = saved.FindIndex(item => item.Key != newType
                        && string.Equals(item.Key, newType, StringComparison.OrdinalIgnoreCase));
                    if (duplicate >= 0) return "Another blueprint already uses this container type.";

                    return SaveText(BlueprintSettingsList.ToIni(saved));
                });

                if (window.ShowModal() == true) Notify();
            });
        }

        private void Notify()
        {
            SettingValuesChanged?.Invoke(this, ExternalSettingsChangedEventArgs.Single(listMoniker));
        }

        /// <summary>
        /// One row per blueprint. The settings page asks for mutable dictionaries because its
        /// Add and Edit dialogs write into them.
        /// </summary>
        private List<IDictionary<string, object>> Rows()
        {
            return BlueprintSettingsList.FromIni(ReadText())
                .Select(item => (IDictionary<string, object>)new Dictionary<string, object>
                {
                    { BlueprintSettingsList.TypeKey, item.Key }
                })
                .ToList();
        }

        private static IEnumerable<string> TypesOf<T>(T value)
        {
            var content = value as ArraySettingContent;
            IEnumerable rows = content != null ? (IEnumerable)content.Items : value as IEnumerable;
            if (rows == null) yield break;

            foreach (object row in rows)
            {
                object type = null;
                var readOnlyRow = row as IReadOnlyDictionary<string, object>;
                if (readOnlyRow != null) readOnlyRow.TryGetValue(BlueprintSettingsList.TypeKey, out type);
                else
                {
                    var writableRow = row as IDictionary<string, object>;
                    if (writableRow != null) writableRow.TryGetValue(BlueprintSettingsList.TypeKey, out type);
                }

                string text = (type as string ?? string.Empty).Trim();
                if (text.Length != 0) yield return text;
            }
        }

        private string ReadText()
        {
            var manager = Manager();
            if (manager != null) return UnifiedBlueprintSettings.Read(manager.GetReader());
            return OptionsPage().LinkedListMemoryBlueprints;
        }

        /// <summary>Returns null when saved, or the message explaining why it was not.</summary>
        private string SaveText(string text)
        {
            var manager = Manager();
            if (manager == null)
            {
                OptionsPage().SaveBlueprints(text);
                return null;
            }
            return UnifiedBlueprintSettings.Write(manager.GetWriter("Neo Watch"), text);
        }

        private ISettingsManager Manager()
        {
            return package.GetService(typeof(UnifiedBlueprintSettings.Service)) as ISettingsManager;
        }

        private BlueprintsOptionPage OptionsPage()
        {
            return (BlueprintsOptionPage)package.GetDialogPage(typeof(BlueprintsOptionPage));
        }

        private static bool IsList(string moniker)
        {
            // Visual Studio resolves monikers inside an external region against the categories the
            // region declares, so match on the leaf: this provider owns a single setting.
            return moniker != null
                && moniker.EndsWith("list", StringComparison.OrdinalIgnoreCase);
        }

        private static Task<ExternalSettingOperationResult> Rejected(string message)
        {
            return Task.FromResult<ExternalSettingOperationResult>(
                new ExternalSettingOperationResult.Failure(message,
                    ExternalSettingsErrorScope.SingleSettingOnly, false));
        }

        private static Task<ExternalSettingOperationResult<T>> Unknown<T>(string moniker)
        {
            return ExternalSettingOperationResult.FailureResultTask<T>(
                "Neo Watch does not own the setting " + moniker + ".",
                ExternalSettingsErrorScope.SingleSettingOnly, false);
        }

        private sealed class EditBlueprintCommand : IArrayItemCommand
        {
            private readonly BlueprintExternalSettings settings;

            public EditBlueprintCommand(BlueprintExternalSettings settings)
            {
                this.settings = settings;
            }

            public string Title { get { return "Edit"; } }
            public string Description { get { return "Edit this blueprint"; } }
            public int DefaultActionPriority { get { return 1000; } }

            public Task<bool> IsEnabledAsync(IDictionary<string, object> item, CancellationToken token)
            {
                return Task.FromResult(true);
            }

            public void Invoke(IDictionary<string, object> item)
            {
                object type;
                item.TryGetValue(BlueprintSettingsList.TypeKey, out type);
                settings.Edit(type as string);
            }
        }
    }
}
