using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;

sealed class NativeDocument
{
    public string Format = "toolkit-native-scene";
    public int Version = 1;
    public string SourceFormat = "venue-toolkit";
    public string SourceVersion = "1";
    public NativeArtboard Artboard = new NativeArtboard();
    public List<NativeNode> Nodes = new List<NativeNode>();
    public List<NativeLayer> Layers = new List<NativeLayer>();
}

sealed class NativeLayer
{
    public string Id;
    public string Name;
    public string ParentId;
    public bool Locked;
    public bool Hidden;
    public bool Targeted;
    public bool Isolated;
}

sealed class NativeArtboard
{
    public double Width = 1200;
    public double Height = 800;
    public string Id = "artboard";
}

sealed class NativeNode
{
    public string Id;
    public string Kind;
    public string Name;
    public double X;
    public double Y;
    public double Width;
    public double Height;
    public double Rotation;
    public string Fill = "#CCCCCC";
    public string Path;
    public bool Locked;
    public bool Hidden;
    public List<string> Children = new List<string>();
    public List<NativePoint> Points = new List<NativePoint>();
    public string Layer = "Layer 1";
}

sealed class NativePoint
{
    public string Id;
    public double X;
    public double Y;
    public double InX;
    public double InY;
    public double OutX;
    public double OutY;
    public string HandleMode = "corner";
}

sealed class NativeExportReport
{
    public List<string> Preserved = new List<string>();
    public List<string> Approximated = new List<string>();
}

sealed class NativeSelectionBounds
{
    public double X;
    public double Y;
    public double Width;
    public double Height;
}

static class NativeSceneGraph
{
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 60000000, RecursionLimit = 150 };

    public static NativeDocument NewDocument(double width, double height)
    {
        var doc = new NativeDocument();
        doc.Artboard.Width = width;
        doc.Artboard.Height = height;
        doc.Layers.Add(new NativeLayer { Id = "layer-1", Name = "Layer 1", ParentId = null, Isolated = false, Targeted = true });
        return doc;
    }

    public static NativeDocument FromVenue(string text)
    {
        if (String.IsNullOrWhiteSpace(text)) throw new InvalidDataException("The venue document is empty.");
        Dictionary<string, object> root;
        try { root = Json.Deserialize<Dictionary<string, object>>(text); }
        catch (Exception ex) { throw new InvalidDataException("The venue document is not valid JSON.", ex); }
        if (root == null || Text(root, "format") != "venue-toolkit") throw new InvalidDataException("Unsupported venue document format.");
        var doc = NewDocument(Number(root, "width", 1), Number(root, "height", 1));
        doc.SourceVersion = Convert.ToString(Value(root, "version"), CultureInfo.InvariantCulture) ?? "1";
        AddShape(doc, "stage", "stage", Map(root, "stage"), "#E0E0E0");
        var zones = List(root, "zones");
        foreach (var zoneObject in zones)
        {
            var zone = zoneObject as Dictionary<string, object>;
            if (zone == null) continue;
            string zoneId = Text(zone, "uid");
            if (String.IsNullOrEmpty(zoneId)) zoneId = StableId("zone", doc.Nodes.Count);
            AddShape(doc, zoneId, "zone", Map(zone, "section"), Text(zone, "color", "#7C9FC1"));
            var seats = List(zone, "seats");
            foreach (var seatObject in seats)
            {
                var seat = seatObject as Dictionary<string, object>;
                if (seat == null) continue;
                string id = Text(seat, "uid");
                if (String.IsNullOrEmpty(id)) id = StableId("seat", doc.Nodes.Count);
                var node = new NativeNode { Id = id, Kind = "seat", Name = Text(seat, "number"), X = Number(seat, "x", 0), Y = Number(seat, "y", 0), Width = Number(seat, "width", 7), Height = Number(seat, "height", 7), Fill = Text(seat, "fill", "#CCCCCC") };
                doc.Nodes.Add(node);
            }
        }
        foreach (var artworkObject in List(root, "artwork"))
        {
            var artwork = artworkObject as Dictionary<string, object>;
            if (artwork == null) continue;
            AddShape(doc, Text(artwork, "uid"), "path", artwork, Text(artwork, "fill", "#63CDE5"));
        }
        return doc;
    }

    public static string Serialize(NativeDocument document)
    {
        if (document == null || document.Artboard == null || document.Nodes == null) throw new InvalidDataException("The native document is incomplete.");
        Validate(document);
        return Json.Serialize(document);
    }

    public static NativeDocument Deserialize(string text)
    {
        NativeDocument document;
        try { document = Json.Deserialize<NativeDocument>(text); }
        catch (Exception ex) { throw new InvalidDataException("The native document is not valid JSON.", ex); }
        Validate(document);
        return document;
    }

    public static NativeDocument FromSvg(string text)
    {
        if (String.IsNullOrWhiteSpace(text)) throw new InvalidDataException("The SVG document is empty.");
        var xml = new XmlDocument { XmlResolver = null };
        try { xml.LoadXml(text); }
        catch (Exception ex) { throw new InvalidDataException("The SVG document is not valid XML.", ex); }
        var root = xml.DocumentElement;
        if (root == null || root.LocalName != "svg") throw new InvalidDataException("The document does not contain an SVG root.");
        double width = AttributeNumber(root, "width", 1200), height = AttributeNumber(root, "height", 800);
        string viewBox = root.GetAttribute("viewBox");
        if (!String.IsNullOrEmpty(viewBox))
        {
            var values = viewBox.Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length == 4)
            {
                width = NumberValue(values[2], width);
                height = NumberValue(values[3], height);
            }
        }
        var doc = NewDocument(Math.Max(1, width), Math.Max(1, height));
        doc.SourceFormat = "svg";
        doc.SourceVersion = "1";
        int index = 0;
        foreach (XmlNode node in root.SelectNodes(".//*"))
        {
            var element = node as XmlElement;
            if (element == null) continue;
            string kind = element.LocalName.ToLowerInvariant();
            if (kind != "rect" && kind != "ellipse" && kind != "path") continue;
            string id = element.GetAttribute("id");
            if (String.IsNullOrEmpty(id)) id = StableId("svg", index);
            if (doc.Nodes.Any(n => n.Id == id)) id = StableId("svg", index + doc.Nodes.Count);
            string fill = element.GetAttribute("fill");
            if (String.IsNullOrEmpty(fill) || fill == "none") fill = "#CCCCCC";
            var shape = new NativeNode { Id = id, Kind = kind, Name = id, Fill = fill, Layer = "layer-1" };
            if (kind == "rect")
            {
                shape.X = AttributeNumber(element, "x", 0); shape.Y = AttributeNumber(element, "y", 0);
                shape.Width = AttributeNumber(element, "width", 0); shape.Height = AttributeNumber(element, "height", 0);
            }
            else if (kind == "ellipse")
            {
                double cx = AttributeNumber(element, "cx", 0), cy = AttributeNumber(element, "cy", 0);
                shape.Width = AttributeNumber(element, "rx", 0) * 2; shape.Height = AttributeNumber(element, "ry", 0) * 2;
                shape.X = cx - shape.Width / 2; shape.Y = cy - shape.Height / 2;
            }
            else
            {
                shape.Path = element.GetAttribute("d");
                var points = ParseSvgPoints(shape.Path);
                if (points.Count > 0)
                {
                    shape.Points = points;
                    shape.X = points.Min(p => p.X); shape.Y = points.Min(p => p.Y);
                    shape.Width = points.Max(p => p.X) - shape.X; shape.Height = points.Max(p => p.Y) - shape.Y;
                }
            }
            if (shape.Width > 0 && shape.Height > 0) doc.Nodes.Add(shape);
            index++;
        }
        return doc;
    }

    static double AttributeNumber(XmlElement element, string name, double fallback)
    {
        return NumberValue(element.GetAttribute(name).Replace("px", ""), fallback);
    }

    static double NumberValue(string value, double fallback)
    {
        double number;
        return Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !Double.IsNaN(number) && !Double.IsInfinity(number) ? number : fallback;
    }

    static List<NativePoint> ParseSvgPoints(string path)
    {
        var points = new List<NativePoint>();
        if (String.IsNullOrWhiteSpace(path)) return points;
        var tokens = Regex.Matches(path, @"[MmLlHhVv]|[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?").Cast<Match>().Select(m => m.Value).ToArray();
        char command = '\0';
        double currentX = 0, currentY = 0;
        for (int i = 0; i < tokens.Length;)
        {
            if (Regex.IsMatch(tokens[i], "^[MmLlHhVv]$"))
            {
                command = tokens[i++][0];
                continue;
            }
            bool relative = Char.IsLower(command);
            char absolute = Char.ToUpperInvariant(command);
            double value;
            if (!Double.TryParse(tokens[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out value)) break;
            if (absolute == 'H' || absolute == 'V')
            {
                if (absolute == 'H') currentX = relative ? currentX + value : value;
                else currentY = relative ? currentY + value : value;
            }
            else
            {
                if (i >= tokens.Length) break;
                double y;
                if (!Double.TryParse(tokens[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) break;
                currentX = relative ? currentX + value : value;
                currentY = relative ? currentY + y : y;
            }
            if (absolute == 'M' || absolute == 'L' || absolute == 'H' || absolute == 'V')
            {
                if (absolute == 'M') command = relative ? 'l' : 'L';
                points.Add(new NativePoint { Id = StableId("point", points.Count), X = currentX, Y = currentY, InX = currentX, InY = currentY, OutX = currentX, OutY = currentY, HandleMode = "corner" });
            }
        }
        return points;
    }

    public static void SaveAtomic(string path, NativeDocument document)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, Serialize(document));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static string ExportSvg(NativeDocument document)
    {
        Validate(document);
        var output = new System.Text.StringBuilder();
        output.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        output.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ");
        output.Append(document.Artboard.Width.ToString(CultureInfo.InvariantCulture)).Append(" ");
        output.Append(document.Artboard.Height.ToString(CultureInfo.InvariantCulture)).Append("\">\n");
        foreach (var node in document.Nodes)
        {
            if (node.Hidden) continue;
            string fill = String.IsNullOrEmpty(node.Fill) ? "#CCCCCC" : node.Fill;
            string transform = node.Rotation == 0 ? "" : " transform=\"rotate(" + node.Rotation.ToString(CultureInfo.InvariantCulture) + " " + (node.X + node.Width / 2).ToString(CultureInfo.InvariantCulture) + " " + (node.Y + node.Height / 2).ToString(CultureInfo.InvariantCulture) + ")\"";
            if (node.Kind == "ellipse")
                output.Append("<ellipse id=\"").Append(Escape(node.Id)).Append("\" cx=\"").Append((node.X + node.Width / 2).ToString(CultureInfo.InvariantCulture)).Append("\" cy=\"").Append((node.Y + node.Height / 2).ToString(CultureInfo.InvariantCulture)).Append("\" rx=\"").Append((node.Width / 2).ToString(CultureInfo.InvariantCulture)).Append("\" ry=\"").Append((node.Height / 2).ToString(CultureInfo.InvariantCulture)).Append("\" fill=\"").Append(Escape(fill)).Append("\"").Append(transform).Append("/>\n");
            else if (!String.IsNullOrEmpty(node.Path))
                output.Append("<path id=\"").Append(Escape(node.Id)).Append("\" d=\"").Append(Escape(node.Path)).Append("\" fill=\"").Append(Escape(fill)).Append(node.Name == "subtract" || node.Name == "exclude" ? "\" fill-rule=\"evenodd\"" : "\"").Append(transform).Append("/>\n");
            else
                output.Append("<rect id=\"").Append(Escape(node.Id)).Append("\" x=\"").Append(node.X.ToString(CultureInfo.InvariantCulture)).Append("\" y=\"").Append(node.Y.ToString(CultureInfo.InvariantCulture)).Append("\" width=\"").Append(node.Width.ToString(CultureInfo.InvariantCulture)).Append("\" height=\"").Append(node.Height.ToString(CultureInfo.InvariantCulture)).Append("\" fill=\"").Append(Escape(fill)).Append("\"").Append(transform).Append("/>\n");
        }
        output.Append("</svg>");
        return output.ToString();
    }

    public static NativeExportReport GetSvgExportReport(NativeDocument document)
    {
        Validate(document);
        var report = new NativeExportReport();
        report.Preserved.Add("artboard");
        report.Preserved.Add("object IDs");
        report.Preserved.Add("fills");
        report.Preserved.Add("rectangles and ellipses");
        report.Preserved.Add("polygon path geometry");
        foreach (NativeNode node in document.Nodes)
        {
            if (node.Rotation != 0) report.Preserved.Add("rotation transforms");
            if (node.Name == "subtract" || node.Name == "exclude") report.Preserved.Add("compound even-odd boolean paths");
            if (node.Points.Any(p => p.HandleMode == "smooth" || p.HandleMode == "symmetric")) report.Preserved.Add("Bezier handle metadata");
        }
        report.Approximated.Add("unsupported effects are omitted");
        return report;
    }

    public static NativeNode Rectangle(double x, double y, double width, double height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Rectangle dimensions must be positive.");
            var points = new[] { new NativePoint { X = x, Y = y }, new NativePoint { X = x + width, Y = y }, new NativePoint { X = x + width, Y = y + height }, new NativePoint { X = x, Y = y + height } };
            return new NativeNode { Id = "rect-" + Guid.NewGuid().ToString("N"), Kind = "rectangle", Name = "Rectangle", X = x, Y = y, Width = width, Height = height, Fill = "#63CDE5", Points = points.ToList(), Path = PathFor(points) };
        }

    public static NativeNode Ellipse(double x, double y, double width, double height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Ellipse dimensions must be positive.");
            return new NativeNode { Id = "ellipse-" + Guid.NewGuid().ToString("N"), Kind = "ellipse", Name = "Ellipse", X = x, Y = y, Width = width, Height = height, Fill = "#63CDE5" };
        }

    public static string PathFor(NativePoint[] points)
        {
            if (points == null || points.Length < 3) throw new ArgumentException("A path needs at least three points.");
            var result = "M";
            for (int i = 0; i < points.Length; i++) result += (i == 0 ? "" : "L") + points[i].X.ToString(CultureInfo.InvariantCulture) + " " + points[i].Y.ToString(CultureInfo.InvariantCulture);
            return result + "Z";
        }

    public static void SetHandleMode(NativeNode node, int pointIndex, string mode)
    {
        if (node == null || node.Points == null || pointIndex < 0 || pointIndex >= node.Points.Count) throw new ArgumentException("The selected node is invalid.");
        if (mode != "corner" && mode != "smooth" && mode != "symmetric") throw new ArgumentException("Unknown handle mode.");
        NativePoint point = node.Points[pointIndex];
        point.HandleMode = mode;
        if (mode == "symmetric")
        {
            double dx = point.OutX - point.X, dy = point.OutY - point.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length == 0) length = Math.Sqrt((point.InX - point.X) * (point.InX - point.X) + (point.InY - point.Y) * (point.InY - point.Y));
            if (length == 0) length = 1;
            double angle = Math.Atan2(dy, dx);
            point.OutX = point.X + Math.Cos(angle) * length;
            point.OutY = point.Y + Math.Sin(angle) * length;
            point.InX = point.X - Math.Cos(angle) * length;
            point.InY = point.Y - Math.Sin(angle) * length;
        }
    }

    public static void MoveHandle(NativeNode node, int pointIndex, bool incoming, double x, double y)
    {
        if (node == null || node.Points == null || pointIndex < 0 || pointIndex >= node.Points.Count) throw new ArgumentException("The selected node is invalid.");
        NativePoint point = node.Points[pointIndex];
        if (point.HandleMode == "corner")
        {
            if (incoming) { point.InX = x; point.InY = y; } else { point.OutX = x; point.OutY = y; }
            return;
        }
        double dx = x - point.X, dy = y - point.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length == 0) length = 1;
        double oppositeLength = incoming
            ? Math.Sqrt((point.OutX - point.X) * (point.OutX - point.X) + (point.OutY - point.Y) * (point.OutY - point.Y))
            : Math.Sqrt((point.InX - point.X) * (point.InX - point.X) + (point.InY - point.Y) * (point.InY - point.Y));
        if (oppositeLength == 0) oppositeLength = length;
        double angle = Math.Atan2(dy, dx);
        if (incoming)
        {
            point.InX = x; point.InY = y;
            point.OutX = point.X - Math.Cos(angle) * (point.HandleMode == "symmetric" ? length : oppositeLength);
            point.OutY = point.Y - Math.Sin(angle) * (point.HandleMode == "symmetric" ? length : oppositeLength);
        }
        else
        {
            point.OutX = x; point.OutY = y;
            point.InX = point.X - Math.Cos(angle) * (point.HandleMode == "symmetric" ? length : oppositeLength);
            point.InY = point.Y - Math.Sin(angle) * (point.HandleMode == "symmetric" ? length : oppositeLength);
        }
    }

    public static void Arrange(NativeDocument document, string id, double x, double y, double width, double height, double rotation)
        {
            NativeNode node = document.Nodes.Find(n => n.Id == id);
            if (node == null || node.Locked) throw new InvalidOperationException("The selected object is unavailable or locked.");
            node.X = x; node.Y = y; node.Width = width; node.Height = height; node.Rotation = rotation;
        }

        public static void Align(NativeDocument document, IList<string> ids, string mode)
        {
            var nodes = document.Nodes.Where(n => ids.Contains(n.Id) && !n.Locked).ToList();
            if (nodes.Count < 2) throw new InvalidOperationException("Align requires at least two unlocked objects.");
            double left = nodes.Min(n => n.X), right = nodes.Max(n => n.X + n.Width), top = nodes.Min(n => n.Y), bottom = nodes.Max(n => n.Y + n.Height);
            foreach (var node in nodes)
            {
                if (mode == "left") node.X = left;
                else if (mode == "right") node.X = right - node.Width;
                else if (mode == "top") node.Y = top;
                else if (mode == "bottom") node.Y = bottom - node.Height;
                else if (mode == "center") node.X = (left + right - node.Width) / 2;
                else if (mode == "middle") node.Y = (top + bottom - node.Height) / 2;
                else throw new ArgumentException("Unknown alignment mode.");
            }
        }

    public static void AlignToArtboard(NativeDocument document, IList<string> ids, string mode)
    {
            var nodes = document.Nodes.Where(n => ids.Contains(n.Id) && !n.Locked).ToList();
            if (nodes.Count == 0) throw new InvalidOperationException("No unlocked objects are selected.");
            foreach (var node in nodes)
            {
                if (mode == "left") node.X = 0;
                else if (mode == "right") node.X = document.Artboard.Width - node.Width;
                else if (mode == "top") node.Y = 0;
                else if (mode == "bottom") node.Y = document.Artboard.Height - node.Height;
                else if (mode == "center") node.X = (document.Artboard.Width - node.Width) / 2;
                else if (mode == "middle") node.Y = (document.Artboard.Height - node.Height) / 2;
                else throw new ArgumentException("Unknown alignment mode.");
            }
        }

    public static void AlignToKey(NativeDocument document, IList<string> ids, string keyId, string mode)
    {
            var key = document.Nodes.Find(n => n.Id == keyId);
            if (key == null) throw new InvalidOperationException("Key object not found.");
            var nodes = document.Nodes.Where(n => ids.Contains(n.Id) && n.Id != keyId && !n.Locked).ToList();
            foreach (var node in nodes)
            {
                if (mode == "left") node.X = key.X;
                else if (mode == "right") node.X = key.X + key.Width - node.Width;
                else if (mode == "top") node.Y = key.Y;
                else if (mode == "bottom") node.Y = key.Y + key.Height - node.Height;
                else if (mode == "center") node.X = key.X + (key.Width - node.Width) / 2;
                else if (mode == "middle") node.Y = key.Y + (key.Height - node.Height) / 2;
                else throw new ArgumentException("Unknown alignment mode.");
            }
        }

    public static NativeSelectionBounds SelectionBounds(NativeDocument document, IList<string> ids)
    {
            var nodes = document.Nodes.Where(n => ids.Contains(n.Id)).ToList();
            if (nodes.Count == 0) throw new InvalidOperationException("No objects are selected.");
            double left = nodes.Min(n => n.X), top = nodes.Min(n => n.Y), right = nodes.Max(n => n.X + n.Width), bottom = nodes.Max(n => n.Y + n.Height);
            return new NativeSelectionBounds { X = left, Y = top, Width = right - left, Height = bottom - top };
    }

    public static void TransformSelection(NativeDocument document, IList<string> ids, double dx, double dy, double scaleX, double scaleY, double rotation)
    {
            var nodes = document.Nodes.Where(n => ids.Contains(n.Id) && !n.Locked).ToList();
            if (nodes.Count == 0) throw new InvalidOperationException("No unlocked objects are selected.");
            double left = nodes.Min(n => n.X), top = nodes.Min(n => n.Y);
            double selectionWidth = nodes.Max(n => n.X + n.Width) - left, selectionHeight = nodes.Max(n => n.Y + n.Height) - top;
            foreach (NativeNode node in nodes)
            {
                node.X = left + (node.X - left) * scaleX + dx;
                node.Y = top + (node.Y - top) * scaleY + dy;
                node.Width *= Math.Abs(scaleX);
                node.Height *= Math.Abs(scaleY);
                node.Rotation += rotation;
                if (node.Points != null && node.Points.Count >= 3)
                {
                    double radians = rotation * Math.PI / 180.0;
                    double centerX = left + dx + selectionWidth * Math.Abs(scaleX) / 2;
                    double centerY = top + dy + selectionHeight * Math.Abs(scaleY) / 2;
                    foreach (NativePoint point in node.Points)
                    {
                        double px = left + (point.X - left) * scaleX + dx;
                        double py = top + (point.Y - top) * scaleY + dy;
                        double rx = px - centerX, ry = py - centerY;
                        point.X = centerX + rx * Math.Cos(radians) - ry * Math.Sin(radians);
                        point.Y = centerY + rx * Math.Sin(radians) + ry * Math.Cos(radians);
                        double inX, inY, outX, outY;
                        TransformPoint(point.InX, point.InY, left, top, scaleX, scaleY, dx, dy, centerX, centerY, radians, out inX, out inY);
                        TransformPoint(point.OutX, point.OutY, left, top, scaleX, scaleY, dx, dy, centerX, centerY, radians, out outX, out outY);
                        point.InX = inX; point.InY = inY; point.OutX = outX; point.OutY = outY;
                    }

                    node.Path = PathFor(node.Points.ToArray());
                }
            }
    }

    static void TransformPoint(double x, double y, double left, double top, double scaleX, double scaleY, double dx, double dy, double centerX, double centerY, double radians, out double resultX, out double resultY)
    {
        double px = left + (x - left) * scaleX + dx - centerX;
        double py = top + (y - top) * scaleY + dy - centerY;
        resultX = centerX + px * Math.Cos(radians) - py * Math.Sin(radians);
        resultY = centerY + px * Math.Sin(radians) + py * Math.Cos(radians);
    }

    public static void Arrange(NativeDocument document, IList<string> ids, string command)
    {
            var selected = document.Nodes.Where(n => ids.Contains(n.Id)).ToList();
            if (selected.Count == 0) throw new InvalidOperationException("No objects are selected.");
            int first = selected.Min(n => document.Nodes.IndexOf(n));
            var moving = selected.OrderBy(n => document.Nodes.IndexOf(n)).ToList();
            foreach (NativeNode node in moving) document.Nodes.Remove(node);
            int insert = command == "front" || command == "forward" ? document.Nodes.Count : 0;
            if (command == "forward") insert = Math.Min(document.Nodes.Count, first + 1);
            if (command == "backward") insert = Math.Max(0, first - 1);
            if (command != "front" && command != "forward" && command != "backward" && command != "back") throw new ArgumentException("Unknown arrange command.");
            document.Nodes.InsertRange(insert, moving);
    }

    public static bool SetLayerParent(NativeDocument document, string layerId, string parentId)
    {
            NativeLayer layer = document.Layers.Find(l => l.Id == layerId);
            if (layer == null || layerId == parentId) return false;
            string cursor = parentId;
            while (!String.IsNullOrEmpty(cursor))
            {
                if (cursor == layerId) return false;
                NativeLayer parent = document.Layers.Find(l => l.Id == cursor);
                cursor = parent == null ? null : parent.ParentId;
            }
            layer.ParentId = parentId;
            return true;
    }

    public static bool ReorderLayer(NativeDocument document, string layerId, string parentId, int siblingIndex)
    {
            NativeLayer layer = document.Layers.Find(l => l.Id == layerId);
            if (layer == null || (parentId != null && !document.Layers.Any(l => l.Id == parentId))) return false;
            if (parentId == layerId) return false;
            string cursor = parentId;
            while (!String.IsNullOrEmpty(cursor))
            {
                if (cursor == layerId) return false;
                NativeLayer parent = document.Layers.Find(l => l.Id == cursor);
                cursor = parent == null ? null : parent.ParentId;
            }
            layer.ParentId = parentId;
            var siblings = document.Layers.Where(l => l.ParentId == parentId && l.Id != layerId).ToList();
            int index = Math.Max(0, Math.Min(siblingIndex, siblings.Count));
            siblings.Insert(index, layer);
            var reordered = document.Layers.Where(l => l.ParentId != parentId).ToList();
            int insertAt = reordered.Count;
            if (siblings.Count > 0)
            {
                NativeLayer anchor = siblings.LastOrDefault();
                int anchorIndex = document.Layers.IndexOf(anchor);
                insertAt = Math.Max(0, Math.Min(anchorIndex, reordered.Count));
            }
            reordered.InsertRange(insertAt, siblings);
            document.Layers = reordered;
            return true;
    }
        public static void SetLayerState(NativeDocument document, string layerId, bool locked, bool hidden, bool targeted)
        {
            NativeLayer layer = document.Layers.Find(l => l.Id == layerId);
            if (layer == null) throw new InvalidOperationException("Layer not found.");
            layer.Locked = locked; layer.Hidden = hidden; layer.Targeted = targeted;
            foreach (var node in document.Nodes.Where(n => n.Layer == layerId)) { node.Locked = locked; node.Hidden = hidden; }
        }

    public static bool EffectiveLayerLocked(NativeDocument document, string layerId)
    {
        return LayerInherits(document, layerId, l => l.Locked);
    }

    public static bool EffectiveLayerHidden(NativeDocument document, string layerId)
    {
        return LayerInherits(document, layerId, l => l.Hidden);
    }

    public static bool EffectiveLayerIsolated(NativeDocument document, string layerId)
    {
        return LayerInherits(document, layerId, l => l.Isolated);
    }

    static bool LayerInherits(NativeDocument document, string layerId, Func<NativeLayer, bool> value)
    {
        var visited = new HashSet<string>();
        string cursor = layerId;
        while (!String.IsNullOrEmpty(cursor) && visited.Add(cursor))
        {
            NativeLayer layer = document.Layers.Find(l => l.Id == cursor);
            if (layer == null) return false;
            if (value(layer)) return true;
            cursor = layer.ParentId;
        }
        return false;
    }

    public static NativeNode Boolean(NativeNode first, NativeNode second, string operation)
        {
            if (first == null || second == null || first.Width <= 0 || first.Height <= 0 || second.Width <= 0 || second.Height <= 0) throw new ArgumentException("Boolean operands must have positive geometry.");
            if (operation != "unite" && operation != "subtract" && operation != "intersect" && operation != "exclude") throw new ArgumentException("Unknown boolean operation.");
            List<NativePoint> a = Polygon(first), b = Polygon(second);
            if (a.Count < 3 || b.Count < 3) throw new ArgumentException("Boolean operands need valid polygon geometry.");
            var result = new NativeNode { Id = "boolean-" + Guid.NewGuid().ToString("N"), Kind = "path", Name = operation, Fill = first.Fill };
            if (operation == "intersect")
            {
                result.Points = ClipConvex(a, b);
                if (result.Points.Count < 3) throw new ArgumentException("Boolean result is degenerate.");
                result.Path = PathFor(result.Points.ToArray());
            }
            else
            {
                result.Points = a;
                result.Path = operation == "subtract" ? CompoundPath(a, Reverse(b)) : CompoundPath(a, b);
            }
            Recalculate(result);
            return result;
        }

    static List<NativePoint> Polygon(NativeNode node)
    {
        if (node.Points != null && node.Points.Count >= 3) return node.Points.Select(ClonePoint).ToList();
        return new List<NativePoint> { new NativePoint { X = node.X, Y = node.Y }, new NativePoint { X = node.X + node.Width, Y = node.Y }, new NativePoint { X = node.X + node.Width, Y = node.Y + node.Height }, new NativePoint { X = node.X, Y = node.Y + node.Height } };
    }
    static List<NativePoint> ClipConvex(List<NativePoint> subject, List<NativePoint> clip)
    {
        List<NativePoint> output = subject;
        for (int i = 0; i < clip.Count && output.Count > 0; i++)
        {
            NativePoint edgeA = clip[i], edgeB = clip[(i + 1) % clip.Count];
            List<NativePoint> input = output; output = new List<NativePoint>();
            NativePoint previous = input[input.Count - 1];
            foreach (NativePoint current in input)
            {
                bool inside = Cross(edgeA, edgeB, current) >= -0.000001, previousInside = Cross(edgeA, edgeB, previous) >= -0.000001;
                if (inside != previousInside) output.Add(LineIntersection(previous, current, edgeA, edgeB));
                if (inside) output.Add(ClonePoint(current));
                previous = current;
            }
        }
        return output;
    }
    static double Cross(NativePoint a, NativePoint b, NativePoint p) { return (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X); }
    static NativePoint LineIntersection(NativePoint a, NativePoint b, NativePoint c, NativePoint d)
    {
        double denominator = (a.X - b.X) * (c.Y - d.Y) - (a.Y - b.Y) * (c.X - d.X);
        if (Math.Abs(denominator) < 0.000001) return ClonePoint(b);
        double t = ((a.X - c.X) * (c.Y - d.Y) - (a.Y - c.Y) * (c.X - d.X)) / denominator;
        return new NativePoint { X = a.X + t * (b.X - a.X), Y = a.Y + t * (b.Y - a.Y) };
    }
    static List<NativePoint> Reverse(List<NativePoint> points) { var result = points.Select(ClonePoint).ToList(); result.Reverse(); return result; }
    static string CompoundPath(List<NativePoint> a, List<NativePoint> b) { return PathFor(a.ToArray()) + " " + PathFor(b.ToArray()); }
    static NativePoint ClonePoint(NativePoint p) { return new NativePoint { Id = p.Id, X = p.X, Y = p.Y, InX = p.InX, InY = p.InY, OutX = p.OutX, OutY = p.OutY, HandleMode = p.HandleMode }; }
    static void Recalculate(NativeNode node) { node.X = node.Points.Min(p => p.X); node.Y = node.Points.Min(p => p.Y); node.Width = node.Points.Max(p => p.X) - node.X; node.Height = node.Points.Max(p => p.Y) - node.Y; }

    static void Validate(NativeDocument document)
    {
        if (document == null || document.Format != "toolkit-native-scene" || document.Version != 1 || document.Artboard == null || document.Nodes == null || document.Layers == null) throw new InvalidDataException("Unsupported native scene version.");
        if (!Finite(document.Artboard.Width) || !Finite(document.Artboard.Height) || document.Artboard.Width <= 0 || document.Artboard.Height <= 0) throw new InvalidDataException("Invalid artboard dimensions.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var layerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var layer in document.Layers)
        {
            if (layer == null || String.IsNullOrEmpty(layer.Id) || !layerIds.Add(layer.Id) || String.IsNullOrEmpty(layer.Name)) throw new InvalidDataException("Native layers require unique IDs and names.");
        }
        foreach (var node in document.Nodes)
        {
            if (node == null || String.IsNullOrEmpty(node.Id) || !ids.Add(node.Id) || String.IsNullOrEmpty(node.Kind)) throw new InvalidDataException("Native nodes require unique IDs and kinds.");
            if (!Finite(node.X) || !Finite(node.Y) || !Finite(node.Width) || !Finite(node.Height) || node.Width < 0 || node.Height < 0) throw new InvalidDataException("Native node geometry is invalid.");
        }
    }

    static void AddShape(NativeDocument doc, string id, string kind, Dictionary<string, object> source, string fill)
    {
        if (source == null) return;
        if (String.IsNullOrEmpty(id)) id = StableId(kind, doc.Nodes.Count);
        var node = new NativeNode { Id = id, Kind = kind, Name = Text(source, "name"), Fill = fill, Path = Text(source, "d"), Rotation = Number(source, "rotation", 0), Locked = Bool(source, "locked"), Hidden = Bool(source, "hidden") };
        var bounds = List(source, "bounds");
        if (bounds.Count == 4) { node.X = Number(bounds[0]); node.Y = Number(bounds[1]); node.Width = Number(bounds[2]) - node.X; node.Height = Number(bounds[3]) - node.Y; }
        var matrix = List(source, "matrix");
        if (matrix.Count == 6) { node.X += Number(matrix[4]); node.Y += Number(matrix[5]); }
        doc.Nodes.Add(node);
    }

    static string StableId(string kind, int index) { return kind + "-" + index.ToString(CultureInfo.InvariantCulture); }
    static object Value(Dictionary<string, object> o, string key) { object value; return o != null && o.TryGetValue(key, out value) ? value : null; }
    static string Text(Dictionary<string, object> o, string key, string fallback = "") { var value = Value(o, key); return value == null ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback; }
    static double Number(Dictionary<string, object> o, string key, double fallback) { return Number(Value(o, key), fallback); }
    static double Number(object value, double fallback = 0) { double result; return value != null && Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out result) ? result : fallback; }
    static Dictionary<string, object> Map(Dictionary<string, object> o, string key) { return Value(o, key) as Dictionary<string, object>; }
    static List<object> List(Dictionary<string, object> o, string key)
    {
        var list = Value(o, key) as IEnumerable;
        var result = new List<object>();
        if (list != null) foreach (object value in list) result.Add(value);
        return result;
    }
    static List<object> List(Dictionary<string, object> o, string key, bool unused = false) { return List(o, key); }
    static bool Bool(Dictionary<string, object> o, string key) { object value = Value(o, key); return value is bool && (bool)value; }
    static string Escape(string value) { return (value ?? "").Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;"); }
    static bool Finite(double value) { return !Double.IsNaN(value) && !Double.IsInfinity(value); }
}
