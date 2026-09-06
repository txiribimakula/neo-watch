using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoWatch.Loading;
using NeoWatch.Settings;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace Tests
{
    [TestClass]
    public class BlueprintSettingsListTests
    {
        private const string Point = "Count=Count\nHead=Head\nNext=Next\nPoint.X=x|Float32\nPoint.Y=y|Float32";

        [TestMethod]
        public void the_page_lists_blueprints_by_container_type_alone()
        {
            Dictionary<string, object> properties = Manifest();
            var region = (Dictionary<string, object>)properties["neoWatch.blueprints.externalSettings"];
            var listing = (Dictionary<string, object>)((Dictionary<string, object>)region["properties"])
                ["blueprints.list"];
            var columns = (Dictionary<string, object>)((Dictionary<string, object>)listing["items"])["properties"];

            // A second column would show a whole blueprint in one grid cell, which is unreadable.
            CollectionAssert.AreEqual(new[] { BlueprintSettingsList.TypeKey }, columns.Keys.ToArray());
            Assert.AreEqual("external", region["type"]);
            Assert.AreEqual(false, listing["allowItemEditing"]);
            Assert.AreEqual(true, listing["allowAdditionsAndRemovals"]);
        }

        [TestMethod]
        public void the_stored_blueprints_keep_their_container_types()
        {
            var store = (Dictionary<string, object>)Manifest()["neoWatch.blueprints.linkedListMemoryBlueprints"];

            var stored = BlueprintSettingsList.FromIni((string)store["default"]);

            Assert.AreEqual(6, stored.Count);
            Assert.AreEqual("DemoSegmentLinkedList", stored[0].Key);
        }

        [TestMethod]
        public void splitting_and_joining_keeps_every_blueprint_loadable()
        {
            string original = "# User notes\n\n[First]\n" + Point + "\n\n; second one\n[Second]\n" + Point;

            var items = BlueprintSettingsList.FromIni(original);
            var parsed = LinkedListMemoryBlueprintParser.Parse(BlueprintSettingsList.ToIni(items));

            Assert.AreEqual(2, items.Count);
            Assert.AreEqual("First", items[0].Key);
            Assert.AreEqual("Second", items[1].Key);
            Assert.AreEqual(2, parsed.Count);
            Assert.IsTrue(parsed[0].Matches("First"));
            Assert.IsTrue(parsed[1].Matches("Second"));
        }

        [TestMethod]
        public void comments_above_a_header_stay_with_that_blueprint()
        {
            var items = BlueprintSettingsList.FromIni("; explains First\n[First]\n" + Point);

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("; explains First\n" + Point, items[0].Value);
        }

        [TestMethod]
        public void entries_without_a_type_are_dropped_instead_of_corrupting_the_text()
        {
            var items = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("  ", Point),
                new KeyValuePair<string, string>("First", Point)
            };

            Assert.AreEqual("[First]\n" + Point, BlueprintSettingsList.ToIni(items));
        }

        [TestMethod]
        public void line_endings_are_normalized_so_the_two_editors_agree()
        {
            var items = BlueprintSettingsList.FromIni("[First]\r\n" + Point.Replace("\n", "\r\n") + "\r\n");

            Assert.AreEqual("[First]\n" + Point, BlueprintSettingsList.ToIni(items));
        }

        private static Dictionary<string, object> Manifest()
        {
            string json;
            using (var stream = typeof(BlueprintSettingsListTests).Assembly
                .GetManifestResourceStream("DemoBlueprintSettings.json"))
            using (var reader = new StreamReader(stream)) json = reader.ReadToEnd();
            var manifest = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }
                .Deserialize<Dictionary<string, object>>(json);
            return (Dictionary<string, object>)manifest["properties"];
        }
    }
}
