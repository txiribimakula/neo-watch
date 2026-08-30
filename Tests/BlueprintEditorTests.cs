using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoWatch.Settings;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace Tests
{
    [TestClass]
    public class BlueprintEditorTests
    {
        private const string Fields = "Count=Count\nHead=Head\nNext=Next\nPoint.X=x|Float32\nPoint.Y=y|Float32";

        [TestMethod]
        public void splits_bundled_blueprints_without_losing_comments_or_content()
        {
            string json;
            using (var stream = typeof(BlueprintEditorTests).Assembly.GetManifestResourceStream("DemoBlueprintSettings.json"))
            using (var reader = new StreamReader(stream)) json = reader.ReadToEnd();
            var manifest = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            var properties = (Dictionary<string, object>)manifest["properties"];
            var setting = (Dictionary<string, object>)properties["neoWatch.general.linkedListMemoryBlueprints"];
            string original = (string)setting["default"];
            var editor = new BlueprintEditorModel(original);

            Assert.AreEqual(6, editor.Entries.Count);
            Assert.AreEqual("DemoSegmentLinkedList", editor.Entries[0].Title);
            foreach (var entry in editor.Entries) Assert.IsFalse(entry.IsExpanded);
            Assert.IsTrue(editor.TrySerialize(out var saved, out var error), error);
            Assert.AreEqual(original, saved);
        }

        [DataTestMethod]
        [DataRow("\n")]
        [DataRow("\r\n")]
        public void preserves_original_line_endings_and_preamble(string newline)
        {
            string original = ("# User notes\n\n[First]\n" + Fields + "\n; next\n[Second]\n" + Fields).Replace("\n", newline);
            var editor = new BlueprintEditorModel(original);
            Assert.IsTrue(editor.TrySerialize(out var saved, out var error), error);
            Assert.AreEqual(original, saved);
        }

        [TestMethod]
        public void comments_before_a_section_stay_with_that_blueprint()
        {
            var editor = new BlueprintEditorModel("[First]\n" + Fields + "\n\n# About second\n[Second]\n" + Fields);
            Assert.IsFalse(editor.Entries[0].Text.Contains("About second"));
            StringAssert.StartsWith(editor.Entries[1].Text, "\n# About second\n");
        }

        [TestMethod]
        public void editing_one_entry_does_not_modify_the_other()
        {
            var editor = new BlueprintEditorModel("[First]\n" + Fields + "\n\n[Second]\n" + Fields);
            string first = editor.Entries[0].Text;
            editor.Entries[1].Text = "[Renamed]\n" + Fields.Replace("x|", "position.x|");
            Assert.AreEqual("Renamed", editor.Entries[1].Title);
            Assert.AreEqual(first, editor.Entries[0].Text);
            Assert.IsTrue(editor.TrySerialize(out var saved, out var error), error);
            Assert.AreEqual(first + editor.Entries[1].Text, saved);
        }

        [TestMethod]
        public void adding_and_removing_entries_keeps_the_others_and_their_order()
        {
            var editor = new BlueprintEditorModel("[First]\n" + Fields);
            var added = editor.Add();
            Assert.IsTrue(added.IsExpanded);
            added.Text = "[Second]\n" + Fields;
            Assert.IsTrue(editor.TrySerialize(out var saved, out var error), error);
            Assert.AreEqual(2, new BlueprintEditorModel(saved).Entries.Count);
            editor.Entries.RemoveAt(0);
            Assert.IsTrue(editor.TrySerialize(out saved, out error), error);
            Assert.AreEqual(added.Text, saved);
            editor.Entries.Clear();
            Assert.IsTrue(editor.TrySerialize(out saved, out error), error);
            Assert.AreEqual(string.Empty, saved);
        }

        [TestMethod]
        public void incomplete_legacy_entries_are_retained_and_not_silently_dropped()
        {
            string original = "# Notes\n[Broken]\nHead=Head\n\n[Good]\n" + Fields;
            var editor = new BlueprintEditorModel(original);
            Assert.AreEqual(2, editor.Entries.Count);
            Assert.AreEqual(original, editor.Entries[0].Text + editor.Entries[1].Text);
            Assert.IsFalse(editor.TrySerialize(out var saved, out var error));
            Assert.IsNull(saved);
            Assert.IsTrue(editor.Entries[0].IsExpanded);
            StringAssert.Contains(error, "Broken");
        }

        [TestMethod]
        public void pasted_multiple_sections_cannot_be_saved_as_one_entry()
        {
            var editor = new BlueprintEditorModel(null);
            editor.Add().Text = "[First]\n" + Fields + "\n[Second]\n" + Fields;
            Assert.IsFalse(editor.TrySerialize(out var saved, out var error));
            Assert.IsNull(saved);
            StringAssert.Contains(error, "exactly one");
        }

        [TestMethod]
        public void duplicate_types_use_the_same_normalization_as_the_loader()
        {
            var editor = new BlueprintEditorModel("[std::vector<Point> ]\n" + Fields);
            editor.Add().Text = "[class std::vector< Point >]\n" + Fields;
            Assert.IsFalse(editor.TrySerialize(out var saved, out var error));
            StringAssert.Contains(error, "already has a blueprint");
        }

        [TestMethod]
        public void empty_new_entry_cannot_overwrite_saved_settings()
        {
            var editor = new BlueprintEditorModel("[First]\n" + Fields);
            editor.Add();
            Assert.IsFalse(editor.TrySerialize(out var saved, out var error));
            Assert.IsNull(saved);
            Assert.AreEqual(2, editor.Entries.Count);
        }

        [TestMethod]
        public void text_without_a_header_is_preserved_for_repair()
        {
            var editor = new BlueprintEditorModel("# Unfinished\nHead=Head");
            Assert.AreEqual(1, editor.Entries.Count);
            Assert.AreEqual("# Unfinished\nHead=Head", editor.Entries[0].Text);
            Assert.IsFalse(editor.TrySerialize(out var saved, out var error));
        }

        [TestMethod]
        public void renaming_a_section_notifies_its_display_title()
        {
            var entry = new BlueprintEntry("[Original]\n" + Fields);
            var notifications = new List<string>();
            entry.PropertyChanged += (sender, e) => notifications.Add(e.PropertyName);
            entry.Text = "[Updated]\n" + Fields;
            CollectionAssert.AreEqual(new[] { "Text", "Title" }, notifications);
            Assert.AreEqual("Updated", entry.Title);
        }
    }
}
