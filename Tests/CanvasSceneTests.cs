using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoWatch.Drawing;
using NeoWatch.Drawing.Scene;
using NeoWatch.Loading;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Point = NeoWatch.Geometries.Point;

namespace Tests
{
    [TestClass]
    public class CanvasSceneTests
    {
        [TestMethod]
        public void NativePrimitiveLayoutIsStable()
        {
            Assert.AreEqual(64, Marshal.SizeOf(typeof(ScenePrimitive)));
            Assert.AreEqual(new IntPtr(56), Marshal.OffsetOf(typeof(ScenePrimitive), nameof(ScenePrimitive.Kind)));
        }

        [TestMethod]
        public void CameraAndSelectionDoNotRebuildScene()
        {
            var collection = Points(10000);
            var scene = collection.CaptureScene();
            collection.SelectedItem = collection[8000];
            collection.NotifyGeometriesChanged();
            Assert.AreSame(scene, collection.CaptureScene());
        }

        [TestMethod]
        public void OneReplacementCopiesOnlyItsBlock()
        {
            var collection = Points(SceneSnapshot.BlockSize * 3);
            var before = collection.CaptureScene();
            collection[SceneSnapshot.BlockSize + 1] = new DrawablePoint(-2, -3);
            var after = collection.CaptureScene();
            Assert.AreSame(before[0], after[0]);
            Assert.AreNotSame(before[1], after[1]);
            Assert.AreSame(before[2], after[2]);
            Assert.AreEqual(-2d, after[1][1].X);
            Assert.AreEqual((double)SceneSnapshot.BlockSize + 1, before[1][1].X);
        }

        [TestMethod]
        public void AppendRemoveAndInsertKeepItemOrder()
        {
            var collection = Points(SceneSnapshot.BlockSize + 2);
            var old = collection.CaptureScene();
            collection.Add(new DrawablePoint(-10, 0));
            Assert.AreSame(old[0], collection.CaptureScene()[0]);
            collection.RemoveAt(0);
            collection.Insert(1, new DrawablePoint(42, 0));
            var scene = collection.CaptureScene();
            for (int i = 0; i < collection.Count; i++)
                Assert.AreEqual((double)((DrawablePoint)collection[i]).X, scene[i / SceneSnapshot.BlockSize][i % SceneSnapshot.BlockSize].X);
        }

        [TestMethod]
        public void SnapshotOwnsValuesAndDoesNotExposeMutableStorage()
        {
            var collection = Points(1);
            var scene = collection.CaptureScene();
            ((DrawablePoint)collection[0]).X = 33;
            var copy = scene[0].CopyPrimitives();
            copy[0] = new ScenePrimitive(PrimitiveKind.Point, -10, -20);
            Assert.AreEqual(0d, scene[0][0].X);
        }

        [DataTestMethod]
        [DataRow(-90, -300)] [DataRow(-720, 360)] [DataRow(45, -360)] [DataRow(15, 1080)]
        public void ArcBoundsNeverCullVisibleArc(int start, int sweep)
        {
            var arc = new ScenePrimitive(PrimitiveKind.Arc, 17, -13, radius: 23, startAngle: start, sweepAngle: sweep);
            for (int i = 0; i <= 100; i++)
            {
                double angle = (start + sweep * i / 100d) * Math.PI / 180;
                double x = 17 + 23 * Math.Cos(angle), y = -13 + 23 * Math.Sin(angle);
                Assert.IsTrue(arc.Bounds.Intersects(new SceneBounds(x, y, x, y)));
            }
        }

        [TestMethod]
        public void BoundsDoNotAliasFirstDrawable()
        {
            var first = new DrawablePoint(0, 0);
            var collection = new DrawableCollection();
            collection.AddAndNotify(new List<IDrawable> { first, new DrawablePoint(100, 100) });
            Assert.AreEqual(1f, first.Box.MaxX);
            collection.CaptureScene();
            collection.ApplyPartialAndNotify(new List<DrawableReplacement>(), new List<IDrawable>(), 1);
            Assert.AreEqual(1f, collection.Box.MaxX);
            Assert.AreEqual(-1f, collection.Box.MinX);
        }

        [TestMethod]
        public void SelectionUsesIdentityEvenForEqualGeometries()
        {
            var item = new WatchItem();
            var a = new DrawablePoint(1, 2); var b = new DrawablePoint(1, 2);
            item.Drawables.AddAndNotify(new List<IDrawable> { a, b });
            item.SelectedItem = b;
            Assert.AreSame(b, item.Drawables.SelectedItem);
            Assert.AreEqual(1, item.Drawables.IndexOfReference(b));
        }

        [TestMethod]
        public void RewindKeepsBothSelectionsAndSharesSnapshot()
        {
            var item = new WatchItem();
            item.Drawables.AddAndNotify(new List<IDrawable> { new DrawablePoint(1, 1), new DrawablePoint(2, 2) });
            var previous = new List<IDrawable>(item.Drawables);
            var scene = item.Drawables.CaptureScene();
            item.Drawables[1] = new DrawablePoint(3, 3);
            item.RememberPreviousDrawables(previous, scene);
            item.SelectedItem = item.Drawables[1];
            item.SetShowingPrevious(true);
            Assert.AreSame(item.Drawables[1], item.Drawables.SelectedItem);
            Assert.AreSame(previous[1], item.PreviousDrawables.SelectedItem);
            Assert.AreSame(scene, item.PreviousDrawables.CaptureScene());
            Assert.IsTrue(item.IsDrawableChanged(previous[1]));
        }

        [TestMethod]
        public void SpatialQueryKeepsStrokeMarginAndOrder()
        {
            var collection = Points(SceneSnapshot.BlockSize * 3);
            var visible = new List<int>();
            var camera = new SceneCamera(2048, 0, 1, 100, 100);
            collection.CaptureScene().VisitVisible(camera.VisibleBounds(), (i, b) => visible.Add(i));
            CollectionAssert.AreEqual(new[] { 0, 1 }, visible);
        }

        [TestMethod]
        public void ClearingSessionDropsBothVersions()
        {
            var item = new WatchItem();
            item.Drawables.Add(new DrawablePoint(1, 2));
            item.Drawables.CaptureScene();
            item.ClearDebugSessionState();
            Assert.AreEqual(0, item.Drawables.CaptureScene().BlockCount);
            Assert.AreEqual(0, item.PreviousDrawables.CaptureScene().BlockCount);
        }

        [TestMethod]
        public void ScenePreparationHonorsCancellation()
        {
            var token = new CancellationToken(true);
            Assert.ThrowsException<OperationCanceledException>(() => SceneSnapshot.Capture(Points(10), cancellationToken: token));
        }

        private static DrawableCollection Points(int count)
        {
            var collection = new DrawableCollection();
            for (int i = 0; i < count; i++) collection.Add(new DrawablePoint(i, 0));
            return collection;
        }
    }
}
