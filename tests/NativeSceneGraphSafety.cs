using System;
using System.IO;
using System.Linq;

class NativeSceneGraphSafety
{
    static void Main()
    {
        string venue = "{\"format\":\"venue-toolkit\",\"version\":1,\"name\":\"Golden\",\"width\":100,\"height\":80,\"stage\":{\"d\":\"M0 0L20 0L20 5L0 5Z\",\"bounds\":[0,0,20,5],\"matrix\":[1,0,0,1,0,0]},\"zones\":[{\"uid\":\"zone00000001\",\"name\":\"A\",\"color\":\"#7C9FC1\",\"section\":{\"d\":\"M10 10L30 10L30 30L10 30Z\",\"bounds\":[10,10,30,30],\"matrix\":[1,0,0,1,0,0]},\"seats\":[{\"uid\":\"seat00000001\",\"number\":1,\"x\":12,\"y\":12,\"width\":7,\"height\":7,\"fill\":\"#CCCCCC\"},{\"uid\":\"seat00000002\",\"number\":2,\"x\":21,\"y\":12,\"width\":7,\"height\":7,\"fill\":\"#CCCCCC\"}]}],\"artwork\":[]}";
        NativeDocument document = NativeSceneGraph.FromVenue(venue);
        if (document.Nodes.Count != 4) throw new Exception("Golden node count changed.");
        if (document.Nodes.Count(n => n.Kind == "seat") != 2) throw new Exception("Golden seat count changed.");
        if (document.Nodes.Single(n => n.Id == "seat00000001").X != 12) throw new Exception("Golden coordinate changed.");
        NativeNode rectangle = NativeSceneGraph.Rectangle(40, 10, 20, 15);
        NativeNode ellipse = NativeSceneGraph.Ellipse(45, 12, 10, 8);
        NativeNode intersection = NativeSceneGraph.Boolean(rectangle, ellipse, "intersect");
        if (intersection.Width <= 0 || intersection.Height <= 0) throw new Exception("Boolean intersection is invalid.");
        rectangle.Points.Clear();
        rectangle.Points.Add(new NativePoint { Id = "p1", X = rectangle.X, Y = rectangle.Y, HandleMode = "corner" });
        rectangle.Points.Add(new NativePoint { Id = "p2", X = rectangle.X + rectangle.Width, Y = rectangle.Y, HandleMode = "smooth" });
        rectangle.Points.Add(new NativePoint { Id = "p3", X = rectangle.X + rectangle.Width, Y = rectangle.Y + rectangle.Height, HandleMode = "corner" });
        rectangle.Points.Add(new NativePoint { Id = "p4", X = rectangle.X, Y = rectangle.Y + rectangle.Height, HandleMode = "corner" });
        if (rectangle.Points[1].HandleMode != "smooth") throw new Exception("Node handle mode did not persist.");
        rectangle.Points[0].InX = 38; rectangle.Points[0].InY = 8; rectangle.Points[0].OutX = 42; rectangle.Points[0].OutY = 8;
        NativeSceneGraph.SetHandleMode(rectangle, 0, "symmetric");
        NativeSceneGraph.MoveHandle(rectangle, 0, false, 45, 10);
        if (Math.Abs(rectangle.Points[0].InX - 35) > 0.001 || Math.Abs(rectangle.Points[0].InY - 10) > 0.001) throw new Exception("Symmetric handle constraint failed.");
        document.Nodes.Add(rectangle);
        document.Nodes.Add(ellipse);
        string svg = NativeSceneGraph.ExportSvg(document);
        if (!svg.Contains("seat00000001") || !svg.Contains("<ellipse")) throw new Exception("Native SVG export lost nodes.");
        NativeNode subtraction = NativeSceneGraph.Boolean(rectangle, ellipse, "subtract");
        if (subtraction.Kind != "path" || !subtraction.Path.Contains("M") || !NativeSceneGraph.ExportSvg(new NativeDocument { Artboard = document.Artboard, Layers = document.Layers, Nodes = new System.Collections.Generic.List<NativeNode> { subtraction } }).Contains("fill-rule=\"evenodd\"")) throw new Exception("Path boolean export lost compound geometry.");
        NativeExportReport exportReport = NativeSceneGraph.GetSvgExportReport(document);
        if (!exportReport.Preserved.Contains("object IDs") || exportReport.Approximated.Count == 0) throw new Exception("SVG capability report is incomplete.");
        NativeSceneGraph.Arrange(document, "seat00000001", 20, 22, 7, 7, 0);
        if (document.Nodes.Single(n => n.Id == "seat00000001").X != 20) throw new Exception("Transform did not persist.");
        document.Nodes[0].Layer = "layer-1";
        NativeSceneGraph.SetLayerState(document, "layer-1", false, true, true);
        if (!document.Nodes[0].Hidden) throw new Exception("Layer visibility did not inherit.");
        NativeSceneGraph.SetLayerState(document, "layer-1", false, false, true);
        NativeSceneGraph.Align(document, new[] { "seat00000001", "seat00000002" }, "top");
        if (document.Nodes.Single(n => n.Id == "seat00000001").Y != document.Nodes.Single(n => n.Id == "seat00000002").Y) throw new Exception("Align did not persist.");
        NativeSceneGraph.AlignToArtboard(document, new[] { "seat00000001", "seat00000002" }, "center");
        if (document.Nodes.Single(n => n.Id == "seat00000001").X != 46.5) throw new Exception("Artboard alignment did not persist.");
        NativeSceneGraph.AlignToKey(document, new[] { "seat00000001", "seat00000002" }, "seat00000001", "top");
        NativeSelectionBounds bounds = NativeSceneGraph.SelectionBounds(document, new[] { "seat00000001", "seat00000002" });
        if (bounds.Width <= 0 || bounds.Height <= 0) throw new Exception("Selection bounds did not update.");
        NativeSceneGraph.TransformSelection(document, new[] { "seat00000001", "seat00000002" }, 5, 3, 1, 1, 10);
        if (document.Nodes.Single(n => n.Id == "seat00000001").X != 51.5) throw new Exception("Group transform did not move selection.");
        NativeNode editablePath = NativeSceneGraph.Rectangle(5, 5, 10, 10);
        double originalPointX = editablePath.Points[0].X;
        NativeSceneGraph.TransformSelection(new NativeDocument { Nodes = new System.Collections.Generic.List<NativeNode> { editablePath } }, new[] { editablePath.Id }, 10, 4, 2, 2, 0);
        if (editablePath.Points[0].X == originalPointX || editablePath.Path.IndexOf("M15", StringComparison.Ordinal) < 0) throw new Exception("Path points did not follow group transform.");
        NativeSceneGraph.Arrange(document, new[] { "seat00000001" }, "front");
        if (document.Nodes[document.Nodes.Count - 1].Id != "seat00000001") throw new Exception("Arrange front did not reorder.");
        NativeLayer child = new NativeLayer { Id = "layer-2", Name = "Child", ParentId = "layer-1" };
        document.Layers.Add(child);
        if (!NativeSceneGraph.SetLayerParent(document, "layer-2", "layer-1") || NativeSceneGraph.SetLayerParent(document, "layer-1", "layer-2")) throw new Exception("Layer cycle prevention failed.");
        NativeSceneGraph.SetLayerState(document, "layer-1", true, true, false);
        if (!NativeSceneGraph.EffectiveLayerLocked(document, "layer-2") || !NativeSceneGraph.EffectiveLayerHidden(document, "layer-2")) throw new Exception("Nested layer inheritance failed.");
        NativeSceneGraph.SetLayerState(document, "layer-1", false, false, true);
        NativeLayer sibling = new NativeLayer { Id = "layer-3", Name = "Sibling", ParentId = null };
        document.Layers.Add(sibling);
        if (!NativeSceneGraph.ReorderLayer(document, "layer-3", null, 0) || document.Layers[0].Id != "layer-3") throw new Exception("Layer sibling reorder failed.");
        bool rejected = false;
        try { NativeSceneGraph.Boolean(rectangle, new NativeNode { Id = "degenerate", Kind = "rectangle", Width = 0, Height = 1 }, "intersect"); } catch (ArgumentException) { rejected = true; }
        if (!rejected) throw new Exception("Degenerate boolean operand was accepted.");
        NativeDocument importedSvg = NativeSceneGraph.FromSvg("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 200 100\"><rect id=\"seat-a\" x=\"10\" y=\"20\" width=\"12\" height=\"8\" fill=\"#123456\"/><ellipse id=\"oval-a\" cx=\"80\" cy=\"40\" rx=\"10\" ry=\"6\"/></svg>");
        if (importedSvg.Nodes.Count != 2 || importedSvg.Nodes.Single(n => n.Id == "seat-a").X != 10 || importedSvg.Nodes.Single(n => n.Id == "oval-a").Width != 20) throw new Exception("SVG bridge did not preserve geometry.");
        NativeDocument relativeSvg = NativeSceneGraph.FromSvg("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 40 40\"><path id=\"relative\" d=\"m2 3 h10 v8 h-10 z\"/></svg>");
        if (relativeSvg.Nodes.Count != 1 || relativeSvg.Nodes[0].Points.Count != 4 || relativeSvg.Nodes[0].Width != 10 || relativeSvg.Nodes[0].Height != 8) throw new Exception("Relative SVG path import did not preserve geometry.");
        string path = Path.Combine(Path.GetTempPath(), "toolkit-native-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            NativeSceneGraph.SaveAtomic(path, document);
            NativeDocument reopened = NativeSceneGraph.Deserialize(File.ReadAllText(path));
            if (reopened.Nodes.Single(n => n.Id == "seat00000002").Y != 15) throw new Exception("Reopened geometry changed.");
            Console.WriteLine("PASS native scene graph golden migration and save/reopen");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
