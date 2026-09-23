using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

sealed class NativeCanvasForm : Form
{
    readonly NativeDocument document;
    readonly string savePath;
    readonly Panel surface;
    readonly Panel inspector;
    readonly ToolStrip toolbar;
    readonly System.Collections.Generic.Stack<string> undo = new System.Collections.Generic.Stack<string>();
    readonly System.Collections.Generic.Stack<string> redo = new System.Collections.Generic.Stack<string>();
    double zoom = 1;
    double panX, panY;
    string selectedId;
    readonly System.Collections.Generic.HashSet<string> selectedIds = new System.Collections.Generic.HashSet<string>();
    int selectedPoint = -1;
    bool selectedIncoming;
    bool selectedHandle;
    string directBefore;
    string selectionBefore;
    PointF selectionStart;
    bool movingSelection;
    bool transformingSelection;
    int transformHandle = -1;
    string transformBefore;
    NativeSelectionBounds transformBounds;
    bool rotatingSelection;
    string rotationBefore;
    NativeSelectionBounds rotationBounds;
    NativeNode booleanPreview;
    string booleanBefore;
    string booleanOperation;
    Point lastMouse;
    bool panning;
    string tool = "select";
    PointF drawStart;
    PointF drawCurrent;
    bool drawing;
    readonly System.Collections.Generic.List<PointF> penPoints = new System.Collections.Generic.List<PointF>();

    public NativeCanvasForm(NativeDocument source, string path)
    {
        document = source;
        savePath = path;
        Text = "Yousufweiji Toolkit · Native Surface";
        Width = 1100;
        Height = 760;
        KeyPreview = true;
        toolbar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
        AddButton("Select", delegate { tool = "select"; CancelDrawing(); });
        AddButton("Direct", delegate { tool = "direct"; CancelDrawing(); });
        AddButton("Rectangle", delegate { tool = "rectangle"; CancelDrawing(); });
        AddButton("Ellipse", delegate { tool = "ellipse"; CancelDrawing(); });
        AddButton("Pen", delegate { tool = "pen"; CancelDrawing(); });
        AddButton("Corner", delegate { SetSelectedHandleMode("corner"); });
        AddButton("Smooth", delegate { SetSelectedHandleMode("smooth"); });
        AddButton("Symmetric", delegate { SetSelectedHandleMode("symmetric"); });
        AddButton("Bring front", delegate { ArrangeSelection("front"); });
        AddButton("Bring forward", delegate { ArrangeSelection("forward"); });
        AddButton("Send backward", delegate { ArrangeSelection("backward"); });
        AddButton("Send back", delegate { ArrangeSelection("back"); });
        AddButton("SVG report", delegate { ShowExportReport(); });
        AddButton("Layers", delegate { ShowLayersPanel(); });
        AddButton("Align artboard", delegate { AlignSelection("center", true); });
        AddButton("Align key", delegate { AlignSelection("left", false); });
        AddButton("Boolean unite", delegate { BeginBoolean("unite"); });
        AddButton("Boolean subtract", delegate { BeginBoolean("subtract"); });
        AddButton("Boolean intersect", delegate { BeginBoolean("intersect"); });
        AddButton("Boolean exclude", delegate { BeginBoolean("exclude"); });
        AddButton("Commit boolean", delegate { CommitBoolean(); });
        AddButton("Cancel boolean", delegate { CancelBoolean(); });
        AddButton("Undo", delegate { Undo(); });
        AddButton("Redo", delegate { Redo(); });
        AddButton("Save", delegate { Save(); });
        AddButton("Export SVG", delegate { ExportSvg(); });
        Controls.Add(toolbar);
        inspector = new Panel { Dock = DockStyle.Right, Width = 220, BackColor = Color.FromArgb(24, 31, 39), Padding = new Padding(8) };
        Controls.Add(inspector);
        surface = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(25, 29, 34), TabStop = true };
        surface.Paint += PaintSurface;
        surface.MouseWheel += Wheel;
        surface.MouseDown += MouseDownSurface;
        surface.MouseMove += MouseMoveSurface;
        surface.MouseUp += MouseUpSurface;
        Controls.Add(surface);
        KeyDown += KeyDownForm;
        FormClosing += (s, e) => { if (!String.IsNullOrEmpty(savePath)) NativeSceneGraph.SaveAtomic(savePath, document); };
    }

    void AddButton(string text, EventHandler handler)
    {
        var button = new ToolStripButton(text);
        button.Click += handler;
        toolbar.Items.Add(button);
    }

    void PaintSurface(object sender, PaintEventArgs e)
    {
        try
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TranslateTransform((float)(panX + 30), (float)(panY + 30));
            e.Graphics.ScaleTransform((float)zoom, (float)zoom);
            using (var background = new SolidBrush(Color.White)) e.Graphics.FillRectangle(background, 0, 0, (float)document.Artboard.Width, (float)document.Artboard.Height);
            using (var border = new Pen(Color.FromArgb(100, 110, 120), 1f / (float)zoom)) e.Graphics.DrawRectangle(border, 0, 0, (float)document.Artboard.Width, (float)document.Artboard.Height);
            bool isolation = document.Layers.Any(l => l.Isolated);
            foreach (var node in document.Nodes.Where(n => !NativeSceneGraph.EffectiveLayerHidden(document, n.Layer) && !n.Hidden))
            {
                bool dimmed = isolation && !NativeSceneGraph.EffectiveLayerIsolated(document, node.Layer);
                RectangleF bounds = new RectangleF((float)node.X, (float)node.Y, (float)node.Width, (float)node.Height);
                Color nodeColor = ParseColor(node.Fill, Color.LightGray);
                if (dimmed) nodeColor = Color.FromArgb(100, nodeColor);
                using (var brush = new SolidBrush(nodeColor))
                {
                    if (node.Kind == "ellipse") e.Graphics.FillEllipse(brush, bounds); else e.Graphics.FillRectangle(brush, bounds);
                    if (!String.IsNullOrEmpty(node.Path)) DrawBasicPath(e.Graphics, node.Path, brush);
                }
                bool selected = node.Id == selectedId || selectedIds.Contains(node.Id);
                using (var pen = new Pen(selected ? Color.FromArgb(0, 120, 215) : Color.FromArgb(90, 90, 90), (selected ? 3f : 1f) / (float)zoom)) { if (node.Kind == "ellipse") e.Graphics.DrawEllipse(pen, bounds); else e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height); }
                if (node.Id == selectedId && tool == "direct" && node.Points != null)
                    foreach (var point in node.Points)
                    {
                        Color modeColor = point.HandleMode == "symmetric" ? Color.MediumPurple : point.HandleMode == "smooth" ? Color.SeaGreen : Color.Orange;
                        using (var handle = new Pen(modeColor, 1f / (float)zoom))
                        using (var dot = new SolidBrush(Color.White))
                        {
                            e.Graphics.DrawLine(handle, (float)point.X, (float)point.Y, (float)point.InX, (float)point.InY);
                            e.Graphics.DrawLine(handle, (float)point.X, (float)point.Y, (float)point.OutX, (float)point.OutY);
                            e.Graphics.FillEllipse(dot, (float)point.X - 4f / (float)zoom, (float)point.Y - 4f / (float)zoom, 8f / (float)zoom, 8f / (float)zoom);
                            e.Graphics.DrawEllipse(handle, (float)point.X - 4f / (float)zoom, (float)point.Y - 4f / (float)zoom, 8f / (float)zoom, 8f / (float)zoom);
                            e.Graphics.FillEllipse(new SolidBrush(modeColor), (float)point.InX - 3f / (float)zoom, (float)point.InY - 3f / (float)zoom, 6f / (float)zoom, 6f / (float)zoom);
                            e.Graphics.FillEllipse(new SolidBrush(modeColor), (float)point.OutX - 3f / (float)zoom, (float)point.OutY - 3f / (float)zoom, 6f / (float)zoom, 6f / (float)zoom);
                        }
                    }
            }
            if (selectedIds.Count > 0)
            {
                try
                {
                    NativeSelectionBounds selectionBounds = NativeSceneGraph.SelectionBounds(document, selectedIds.ToList());
                    using (var selectionPen = new Pen(Color.FromArgb(0, 120, 215), 1.5f / (float)zoom))
                    using (var handleBrush = new SolidBrush(Color.White))
                    {
                        e.Graphics.DrawRectangle(selectionPen, (float)selectionBounds.X, (float)selectionBounds.Y, (float)selectionBounds.Width, (float)selectionBounds.Height);
                        foreach (PointF handle in new[]
                        {
                            new PointF((float)selectionBounds.X, (float)selectionBounds.Y),
                            new PointF((float)(selectionBounds.X + selectionBounds.Width), (float)selectionBounds.Y),
                            new PointF((float)selectionBounds.X, (float)(selectionBounds.Y + selectionBounds.Height)),
                            new PointF((float)(selectionBounds.X + selectionBounds.Width), (float)(selectionBounds.Y + selectionBounds.Height))
                        })
                        {
                            float size = 8f / (float)zoom;
                            e.Graphics.FillRectangle(handleBrush, handle.X - size / 2, handle.Y - size / 2, size, size);
                            e.Graphics.DrawRectangle(selectionPen, handle.X - size / 2, handle.Y - size / 2, size, size);
                        }
                    }
                    PointF rotationHandle = new PointF((float)(selectionBounds.X + selectionBounds.Width / 2), (float)(selectionBounds.Y - 24 / zoom));
                    using (var rotationPen = new Pen(Color.FromArgb(180, 80, 80, 80), 1f / (float)zoom))
                    using (var rotationBrush = new SolidBrush(Color.FromArgb(255, 255, 210, 80)))
                    {
                        e.Graphics.DrawLine(rotationPen, (float)(selectionBounds.X + selectionBounds.Width / 2), (float)selectionBounds.Y, rotationHandle.X, rotationHandle.Y);
                        e.Graphics.FillEllipse(rotationBrush, rotationHandle.X - 5 / (float)zoom, rotationHandle.Y - 5 / (float)zoom, 10 / (float)zoom, 10 / (float)zoom);
                        e.Graphics.DrawEllipse(rotationPen, rotationHandle.X - 5 / (float)zoom, rotationHandle.Y - 5 / (float)zoom, 10 / (float)zoom, 10 / (float)zoom);
                    }
                }
                catch (InvalidOperationException) { }
            }
            if (penPoints.Count > 1)
                using (var pen = new Pen(Color.FromArgb(0, 120, 215), 2f / (float)zoom)) e.Graphics.DrawLines(pen, penPoints.ToArray());
            if (drawing && (tool == "rectangle" || tool == "ellipse"))
            {
                float x = Math.Min(drawStart.X, drawCurrent.X), y = Math.Min(drawStart.Y, drawCurrent.Y), w = Math.Abs(drawCurrent.X - drawStart.X), h = Math.Abs(drawCurrent.Y - drawStart.Y);
                using (var pen = new Pen(Color.FromArgb(0, 120, 215), 1f / (float)zoom)) { if (tool == "ellipse") e.Graphics.DrawEllipse(pen, x, y, w, h); else e.Graphics.DrawRectangle(pen, x, y, w, h); }
            }
            if (booleanPreview != null)
            {
                RectangleF previewBounds = new RectangleF((float)booleanPreview.X, (float)booleanPreview.Y, (float)booleanPreview.Width, (float)booleanPreview.Height);
                using (var previewBrush = new SolidBrush(Color.FromArgb(110, 40, 180, 120)))
                using (var previewPen = new Pen(Color.FromArgb(230, 20, 150, 110), 2f / (float)zoom))
                {
                    if (!String.IsNullOrEmpty(booleanPreview.Path)) DrawBasicPath(e.Graphics, booleanPreview.Path, previewBrush);
                    e.Graphics.DrawRectangle(previewPen, previewBounds.X, previewBounds.Y, previewBounds.Width, previewBounds.Height);
                }
            }
        }
        catch (ExternalException)
        {
            RecoverRenderer();
        }
    }

    void RecoverRenderer()
    {
        surface.BackColor = Color.FromArgb(25, 29, 34);
        surface.Invalidate();
    }

    static void DrawBasicPath(Graphics graphics, string path, Brush fill)
    {
        using (var geometry = new GraphicsPath(FillMode.Alternate))
        {
            string[] subpaths = path.Split(new[] { 'M', 'm' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string subpath in subpaths)
            {
                string[] parts = subpath.Replace("L", " ").Replace("l", " ").Replace("Z", " ").Replace("z", " ").Replace(",", " ").Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var points = new System.Collections.Generic.List<PointF>();
                for (int i = 0; i + 1 < parts.Length; i += 2)
                {
                    float x, y;
                    if (Single.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) && Single.TryParse(parts[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y)) points.Add(new PointF(x, y));
                }
                if (points.Count >= 3) geometry.AddPolygon(points.ToArray());
            }
            graphics.FillPath(fill, geometry);
        }
    }

    void Wheel(object sender, MouseEventArgs e)
    {
        double factor = e.Delta > 0 ? 1.1 : 0.9;
        zoom = Math.Max(.05, Math.Min(16, zoom * factor));
        surface.Invalidate();
    }

    void MouseDownSurface(object sender, MouseEventArgs e)
    {
        lastMouse = e.Location;
        if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right) { panning = true; surface.Cursor = Cursors.SizeAll; return; }
        if (e.Button != MouseButtons.Left) return;
        PointF p = DocumentPoint(e.Location);
        if (tool == "rectangle" || tool == "ellipse") { drawStart = p; drawCurrent = p; drawing = true; surface.Invalidate(); return; }
        if (tool == "pen") { penPoints.Add(p); surface.Invalidate(); return; }
        if (tool == "select" && selectedIds.Count > 0)
        {
            NativeSelectionBounds bounds = NativeSceneGraph.SelectionBounds(document, selectedIds.ToList());
            PointF rotationHandle = new PointF((float)(bounds.X + bounds.Width / 2), (float)(bounds.Y - 24 / zoom));
            if (Math.Abs(rotationHandle.X - p.X) <= 10 / zoom && Math.Abs(rotationHandle.Y - p.Y) <= 10 / zoom)
            {
                rotationBefore = NativeSceneGraph.Serialize(document);
                rotationBounds = bounds;
                rotatingSelection = true;
                selectionStart = p;
                return;
            }
            PointF[] handles = { new PointF((float)bounds.X, (float)bounds.Y), new PointF((float)(bounds.X + bounds.Width), (float)bounds.Y), new PointF((float)bounds.X, (float)(bounds.Y + bounds.Height)), new PointF((float)(bounds.X + bounds.Width), (float)(bounds.Y + bounds.Height)) };
            transformHandle = Array.FindIndex(handles, h => Math.Abs(h.X - p.X) <= 10 / zoom && Math.Abs(h.Y - p.Y) <= 10 / zoom);
            if (transformHandle >= 0)
            {
                transformBefore = NativeSceneGraph.Serialize(document);
                transformBounds = bounds;
                transformingSelection = true;
                selectionStart = p;
                return;
            }
        }
        NativeNode hit = document.Nodes.AsEnumerable().Reverse().FirstOrDefault(n => new RectangleF((float)n.X, (float)n.Y, (float)n.Width, (float)n.Height).Contains(p));
        if (tool == "direct" && hit != null && hit.Points != null)
        {
            selectedId = hit.Id;
            selectedHandle = false;
            selectedPoint = hit.Points.FindIndex(q => (Math.Abs(q.InX - p.X) <= 8 / zoom && Math.Abs(q.InY - p.Y) <= 8 / zoom) || (Math.Abs(q.OutX - p.X) <= 8 / zoom && Math.Abs(q.OutY - p.Y) <= 8 / zoom));
            if (selectedPoint >= 0)
            {
                NativePoint hp = hit.Points[selectedPoint];
                selectedIncoming = Math.Abs(hp.InX - p.X) + Math.Abs(hp.InY - p.Y) < Math.Abs(hp.OutX - p.X) + Math.Abs(hp.OutY - p.Y);
                selectedHandle = Math.Abs((selectedIncoming ? hp.InX : hp.OutX) - p.X) <= 8 / zoom && Math.Abs((selectedIncoming ? hp.InY : hp.OutY) - p.Y) <= 8 / zoom;
            }
            if (!selectedHandle) selectedPoint = hit.Points.FindIndex(q => Math.Abs(q.X - p.X) <= 8 / zoom && Math.Abs(q.Y - p.Y) <= 8 / zoom);
            directBefore = NativeSceneGraph.Serialize(document);
            surface.Invalidate();
            return;
        }
        if (Control.ModifierKeys != Keys.Control) selectedIds.Clear();
        selectedId = hit == null ? null : hit.Id;
        if (hit != null) selectedIds.Add(hit.Id);
        if (hit != null && tool == "select")
        {
            selectionStart = p;
            selectionBefore = NativeSceneGraph.Serialize(document);
            movingSelection = true;
        }
        BuildInspector();
        surface.Invalidate();
    }

    void MouseMoveSurface(object sender, MouseEventArgs e)
    {
        if (drawing && (tool == "rectangle" || tool == "ellipse")) { drawCurrent = DocumentPoint(e.Location); surface.Invalidate(); return; }
        if (transformingSelection)
        {
            double x = transformBounds.X, y = transformBounds.Y, right = transformBounds.X + transformBounds.Width, bottom = transformBounds.Y + transformBounds.Height;
            if (transformHandle == 0) { x = Math.Min(right - 1, DocumentPoint(e.Location).X); y = Math.Min(bottom - 1, DocumentPoint(e.Location).Y); }
            if (transformHandle == 1) { right = Math.Max(x + 1, DocumentPoint(e.Location).X); y = Math.Min(bottom - 1, DocumentPoint(e.Location).Y); }
            if (transformHandle == 2) { x = Math.Min(right - 1, DocumentPoint(e.Location).X); bottom = Math.Max(y + 1, DocumentPoint(e.Location).Y); }
            if (transformHandle == 3) { right = Math.Max(x + 1, DocumentPoint(e.Location).X); bottom = Math.Max(y + 1, DocumentPoint(e.Location).Y); }
            Restore(transformBefore);
            NativeSceneGraph.TransformSelection(document, selectedIds.ToList(), x - transformBounds.X, y - transformBounds.Y, (right - x) / transformBounds.Width, (bottom - y) / transformBounds.Height, 0);
            surface.Invalidate();
            return;
        }
        if (rotatingSelection)
        {
            PointF current = DocumentPoint(e.Location);
            double centerX = rotationBounds.X + rotationBounds.Width / 2;
            double centerY = rotationBounds.Y + rotationBounds.Height / 2;
            double startAngle = Math.Atan2(selectionStart.Y - centerY, selectionStart.X - centerX) * 180 / Math.PI;
            double currentAngle = Math.Atan2(current.Y - centerY, current.X - centerX) * 180 / Math.PI;
            Restore(rotationBefore);
            NativeSceneGraph.TransformSelection(document, selectedIds.ToList(), 0, 0, 1, 1, currentAngle - startAngle);
            surface.Invalidate();
            return;
        }
        if (tool == "direct" && selectedPoint >= 0 && selectedId != null)
        {
            NativeNode node = document.Nodes.Find(n => n.Id == selectedId);
            if (node != null && selectedPoint < node.Points.Count)
            {
                PointF current = DocumentPoint(e.Location);
                NativePoint point = node.Points[selectedPoint];
                if (selectedHandle) NativeSceneGraph.MoveHandle(node, selectedPoint, selectedIncoming, current.X, current.Y);
                else { point.X = current.X; point.Y = current.Y; }
                node.Path = NativeSceneGraph.PathFor(node.Points.ToArray());
                RecalculateBounds(node);
                surface.Invalidate();
            }
            return;
        }
        if (movingSelection && tool == "select")
        {
            PointF current = DocumentPoint(e.Location);
            NativeSceneGraph.TransformSelection(document, selectedIds.ToList(), current.X - selectionStart.X, current.Y - selectionStart.Y, 1, 1, 0);
            selectionStart = current;
            surface.Invalidate();
            return;
        }
        if (!panning) return;
        panX += e.X - lastMouse.X;
        panY += e.Y - lastMouse.Y;
        lastMouse = e.Location;
        surface.Invalidate();
    }

    void MouseUpSurface(object sender, MouseEventArgs e)
    {
        if (drawing && (tool == "rectangle" || tool == "ellipse"))
        {
            PointF end = DocumentPoint(e.Location);
            double x = Math.Min(drawStart.X, end.X), y = Math.Min(drawStart.Y, end.Y), w = Math.Abs(end.X - drawStart.X), h = Math.Abs(end.Y - drawStart.Y);
            drawing = false;
            if (w >= 1 && h >= 1)
            {
                Snapshot();
                document.Nodes.Add(tool == "ellipse" ? NativeSceneGraph.Ellipse(x, y, w, h) : NativeSceneGraph.Rectangle(x, y, w, h));
            }
            surface.Invalidate();
        }
        if (panning) { panning = false; surface.Cursor = Cursors.Default; }
        if (movingSelection)
        {
            if (selectionBefore != null) { undo.Push(selectionBefore); redo.Clear(); }
            movingSelection = false; selectionBefore = null;
        }
        if (transformingSelection)
        {
            if (transformBefore != null) { undo.Push(transformBefore); redo.Clear(); }
            transformingSelection = false; transformBefore = null; transformHandle = -1;
        }
        if (rotatingSelection)
        {
            if (rotationBefore != null) { undo.Push(rotationBefore); redo.Clear(); }
            rotatingSelection = false; rotationBefore = null;
        }
        if (tool == "direct" && selectedPoint >= 0) { if (directBefore != null) { undo.Push(directBefore); redo.Clear(); } selectedPoint = -1; directBefore = null; }
    }
    void KeyDownForm(object sender, KeyEventArgs e)
    {
        if (!e.Control && !e.Alt)
        {
            if (e.KeyCode == Keys.V) { tool = "select"; CancelDrawing(); }
            if (e.KeyCode == Keys.A) { tool = "direct"; CancelDrawing(); }
            if (e.KeyCode == Keys.P) { tool = "pen"; CancelDrawing(); }
            if (e.KeyCode == Keys.R) { tool = "rectangle"; CancelDrawing(); }
            if (e.KeyCode == Keys.E) { tool = "ellipse"; CancelDrawing(); }
        }
        if (e.KeyCode == Keys.S && e.Control) { NativeSceneGraph.SaveAtomic(savePath, document); e.SuppressKeyPress = true; }
        if (e.KeyCode == Keys.Enter && tool == "pen" && penPoints.Count >= 3) { Snapshot(); AddPenPath(); e.SuppressKeyPress = true; }
        if (e.KeyCode == Keys.Escape)
        {
            CancelBoolean();
            if (transformingSelection && transformBefore != null) Restore(transformBefore);
            transformingSelection = false; transformBefore = null; transformHandle = -1;
            if (rotatingSelection && rotationBefore != null) Restore(rotationBefore);
            rotatingSelection = false; rotationBefore = null;
            if (movingSelection && selectionBefore != null) Restore(selectionBefore);
            movingSelection = false; selectionBefore = null;
            if (directBefore != null) { Restore(directBefore); directBefore = null; selectedPoint = -1; selectedHandle = false; }
            CancelDrawing();
        }
        if (e.KeyCode == Keys.Z && e.Control) { Undo(); e.SuppressKeyPress = true; }
        if (e.KeyCode == Keys.Y && e.Control) { Redo(); e.SuppressKeyPress = true; }
        if (e.KeyCode == Keys.F && e.Control) { zoom = Math.Min((surface.ClientSize.Width - 60) / document.Artboard.Width, (surface.ClientSize.Height - 60) / document.Artboard.Height); panX = 0; panY = 0; surface.Invalidate(); }
        if (selectedIds.Count > 0 && e.KeyCode == Keys.Delete) { Snapshot(); document.Nodes.RemoveAll(n => selectedIds.Contains(n.Id)); selectedIds.Clear(); selectedId = null; surface.Invalidate(); }
        if (selectedId != null && e.KeyCode == Keys.Left) MoveSelected(-1, 0);
        if (selectedId != null && e.KeyCode == Keys.Right) MoveSelected(1, 0);
        if (selectedId != null && e.KeyCode == Keys.Up) MoveSelected(0, -1);
        if (selectedId != null && e.KeyCode == Keys.Down) MoveSelected(0, 1);
        if (e.KeyCode == Keys.T && selectedId != null) BuildInspector();
    }
    PointF DocumentPoint(Point p) { return new PointF((float)((p.X - panX - 30) / zoom), (float)((p.Y - panY - 30) / zoom)); }
    static Color ParseColor(string value, Color fallback) { try { return ColorTranslator.FromHtml(value); } catch { return fallback; } }
    void Snapshot() { undo.Push(NativeSceneGraph.Serialize(document)); redo.Clear(); }
    void Save() { NativeSceneGraph.SaveAtomic(savePath, document); }
    void AlignSelection(string mode, bool artboard)
    {
        if (selectedIds.Count == 0) return;
        Snapshot();
        if (artboard) NativeSceneGraph.AlignToArtboard(document, selectedIds.ToList(), mode);
        else NativeSceneGraph.AlignToKey(document, selectedIds.ToList(), selectedId, mode);
        surface.Invalidate();
    }
    void BeginBoolean(string operation)
    {
        if (selectedIds.Count != 2) { MessageBox.Show("Select exactly two objects first.", "Boolean preview", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var nodes = document.Nodes.Where(n => selectedIds.Contains(n.Id)).ToList();
        try
        {
            booleanBefore = NativeSceneGraph.Serialize(document);
            booleanOperation = operation;
            booleanPreview = NativeSceneGraph.Boolean(nodes[0], nodes[1], operation);
            surface.Invalidate();
        }
        catch (Exception ex) { booleanBefore = null; booleanPreview = null; MessageBox.Show(ex.Message, "Boolean preview", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    void CommitBoolean()
    {
        if (booleanPreview == null) return;
        Snapshot();
        document.Nodes.RemoveAll(n => selectedIds.Contains(n.Id));
        document.Nodes.Add(booleanPreview);
        selectedIds.Clear(); selectedIds.Add(booleanPreview.Id); selectedId = booleanPreview.Id;
        booleanPreview = null; booleanBefore = null; booleanOperation = null;
        surface.Invalidate();
    }
    void CancelBoolean()
    {
        if (booleanPreview == null) return;
        if (booleanBefore != null) Restore(booleanBefore);
        booleanPreview = null; booleanBefore = null; booleanOperation = null;
        surface.Invalidate();
    }
    void ArrangeSelection(string command)
    {
        if (selectedIds.Count == 0) return;
        Snapshot();
        NativeSceneGraph.Arrange(document, selectedIds.ToList(), command);
        surface.Invalidate();
    }
    void ShowExportReport()
    {
        NativeExportReport report = NativeSceneGraph.GetSvgExportReport(document);
        MessageBox.Show("Preserved:\n- " + String.Join("\n- ", report.Preserved) + "\n\nApproximated or omitted:\n- " + String.Join("\n- ", report.Approximated), "SVG export capability", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    void ShowLayersPanel()
    {
        using (var dialog = new Form { Text = "Layers and groups", Width = 420, Height = 420, StartPosition = FormStartPosition.CenterParent })
        {
            var tree = new TreeView { Dock = DockStyle.Fill, CheckBoxes = true };
            var roots = new System.Collections.Generic.Dictionary<string, TreeNode>();
            var breadcrumb = new Label { Dock = DockStyle.Top, Height = 24, ForeColor = Color.White, Text = "Isolation: document" };
            foreach (NativeLayer layer in document.Layers)
            {
                TreeNode node = new TreeNode(LayerCaption(layer)) { Name = layer.Id, Checked = !layer.Hidden, Tag = layer };
                if (!String.IsNullOrEmpty(layer.ParentId) && roots.ContainsKey(layer.ParentId)) roots[layer.ParentId].Nodes.Add(node); else tree.Nodes.Add(node);
                roots[layer.Id] = node;
            }
            tree.ItemDrag += delegate(object sender, ItemDragEventArgs args) { tree.DoDragDrop(args.Item, DragDropEffects.Move); };
            tree.AllowDrop = true;
            tree.DragOver += delegate(object sender, DragEventArgs args)
            {
                Point point = tree.PointToClient(new Point(args.X, args.Y));
                TreeNode target = tree.GetNodeAt(point);
                args.Effect = target == null ? DragDropEffects.None : DragDropEffects.Move;
                if (target != null) breadcrumb.Text = "Drop " + (point.Y < target.Bounds.Top + target.Bounds.Height / 2 ? "before " : "after ") + target.Text;
            };
            tree.DragDrop += delegate(object sender, DragEventArgs args)
            {
                Point point = tree.PointToClient(new Point(args.X, args.Y));
                TreeNode target = tree.GetNodeAt(point);
                TreeNode source = args.Data.GetData(typeof(TreeNode)) as TreeNode;
                if (source == null || target == null || source == target || source.Nodes.Contains(target)) return;
                NativeLayer sourceLayer = source.Tag as NativeLayer, targetLayer = target.Tag as NativeLayer;
                if (sourceLayer == null || targetLayer == null) return;
                Snapshot();
                bool before = point.Y < target.Bounds.Top + target.Bounds.Height / 2;
                string parentId = targetLayer.ParentId;
                var siblings = document.Layers.Where(l => l.ParentId == parentId && l.Id != sourceLayer.Id).ToList();
                int targetIndex = siblings.FindIndex(l => l.Id == targetLayer.Id);
                int insertion = Math.Max(0, targetIndex + (before ? 0 : 1));
                if (!NativeSceneGraph.ReorderLayer(document, sourceLayer.Id, parentId, insertion)) { Undo(); return; }
                TreeNodeCollection collection = target.Parent == null ? tree.Nodes : target.Parent.Nodes;
                source.Remove();
                insertion = Math.Max(0, Math.Min(insertion, collection.Count));
                collection.Insert(insertion, source);
                tree.SelectedNode = source;
                breadcrumb.Text = "Isolation: document · reordered";
                surface.Invalidate();
            };
            tree.AfterSelect += delegate(object sender, TreeViewEventArgs args)
            {
                NativeLayer layer = args.Node.Tag as NativeLayer;
                breadcrumb.Text = layer != null && layer.Isolated ? "Isolation: document / " + layer.Name : "Isolation: document";
                selectedIds.Clear();
                if (layer != null)
                    foreach (NativeNode node in document.Nodes.Where(n => n.Layer == layer.Id)) selectedIds.Add(node.Id);
                selectedId = selectedIds.Count == 1 ? selectedIds.First() : null;
                surface.Invalidate();
            };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44 };
            var apply = new Button { Text = "Apply visibility / isolation" };
            var isolate = new Button { Text = "Toggle isolation" };
            var lockButton = new Button { Text = "Toggle lock" };
            var targetButton = new Button { Text = "Toggle target" };
            var selectObjects = new Button { Text = "Select objects" };
            var addLayer = new Button { Text = "New layer" };
            var renameLayer = new Button { Text = "Rename" };
            var deleteLayer = new Button { Text = "Delete layer" };
            var exitIsolation = new Button { Text = "Exit isolation" };
            buttons.Height = 76;
            apply.Click += delegate
            {
                Snapshot();
                foreach (TreeNode item in Flatten(tree.Nodes))
                {
                    NativeLayer layer = item.Tag as NativeLayer;
                    if (layer != null) { layer.Hidden = !item.Checked; item.Text = LayerCaption(layer); }
                }
                surface.Invalidate();
            };
            isolate.Click += delegate
            {
                TreeNode item = tree.SelectedNode;
                if (item == null) return;
                NativeLayer layer = item.Tag as NativeLayer;
                Snapshot();
                layer.Isolated = !layer.Isolated;
                tree.SelectedNode.Text = LayerCaption(layer);
                surface.Invalidate();
            };
            lockButton.Click += delegate
            {
                NativeLayer layer = tree.SelectedNode == null ? null : tree.SelectedNode.Tag as NativeLayer;
                if (layer == null) return;
                Snapshot(); layer.Locked = !layer.Locked;
                foreach (NativeNode node in document.Nodes.Where(n => n.Layer == layer.Id)) node.Locked = layer.Locked;
                tree.SelectedNode.Text = LayerCaption(layer);
            };
            targetButton.Click += delegate
            {
                NativeLayer layer = tree.SelectedNode == null ? null : tree.SelectedNode.Tag as NativeLayer;
                if (layer == null) return;
                Snapshot(); layer.Targeted = !layer.Targeted;
                tree.SelectedNode.Text = LayerCaption(layer);
            };
            selectObjects.Click += delegate
            {
                TreeNode item = tree.SelectedNode;
                NativeLayer layer = item == null ? null : item.Tag as NativeLayer;
                if (layer == null) return;
                selectedIds.Clear();
                foreach (NativeNode node in document.Nodes.Where(n => n.Layer == layer.Id)) selectedIds.Add(node.Id);
                selectedId = selectedIds.Count == 1 ? selectedIds.First() : null;
                surface.Invalidate();
            };
            addLayer.Click += delegate
            {
                string name = "Layer " + (document.Layers.Count + 1);
                var layer = new NativeLayer { Id = "layer-" + Guid.NewGuid().ToString("N"), Name = name, ParentId = tree.SelectedNode == null ? null : ((NativeLayer)tree.SelectedNode.Tag).ParentId, Targeted = true };
                Snapshot();
                document.Layers.Add(layer);
                var node = new TreeNode(LayerCaption(layer)) { Name = layer.Id, Checked = true, Tag = layer };
                if (tree.SelectedNode != null && tree.SelectedNode.Parent != null) tree.SelectedNode.Parent.Nodes.Add(node); else tree.Nodes.Add(node);
                tree.SelectedNode = node;
                surface.Invalidate();
            };
            renameLayer.Click += delegate
            {
                NativeLayer layer = tree.SelectedNode == null ? null : tree.SelectedNode.Tag as NativeLayer;
                if (layer == null) return;
                using (var prompt = new Form { Text = "Rename layer", Width = 320, Height = 130, StartPosition = FormStartPosition.CenterParent })
                {
                    var box = new TextBox { Dock = DockStyle.Top, Text = layer.Name };
                    var ok = new Button { Text = "Rename", Dock = DockStyle.Bottom, DialogResult = DialogResult.OK };
                    prompt.Controls.Add(box); prompt.Controls.Add(ok); prompt.AcceptButton = ok;
                    if (prompt.ShowDialog(dialog) == DialogResult.OK && !String.IsNullOrWhiteSpace(box.Text))
                    {
                        Snapshot(); layer.Name = box.Text.Trim(); tree.SelectedNode.Text = LayerCaption(layer); breadcrumb.Text = "Isolation: document";
                    }
                }
            };
            deleteLayer.Click += delegate
            {
                TreeNode item = tree.SelectedNode;
                NativeLayer layer = item == null ? null : item.Tag as NativeLayer;
                if (layer == null || document.Layers.Count <= 1) return;
                if (document.Layers.Any(l => l.ParentId == layer.Id)) { MessageBox.Show("Move or delete child layers first.", "Layers", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                Snapshot();
                document.Layers.Remove(layer);
                foreach (NativeNode node in document.Nodes.Where(n => n.Layer == layer.Id)) node.Layer = document.Layers[0].Id;
                item.Remove();
                selectedIds.Clear(); selectedId = null; surface.Invalidate();
            };
            exitIsolation.Click += delegate
            {
                Snapshot();
                foreach (NativeLayer layer in document.Layers) layer.Isolated = false;
                breadcrumb.Text = "Isolation: document";
                surface.Invalidate();
            };
            buttons.Controls.Add(apply); buttons.Controls.Add(isolate); buttons.Controls.Add(lockButton); buttons.Controls.Add(targetButton);
            buttons.Controls.Add(selectObjects); buttons.Controls.Add(addLayer); buttons.Controls.Add(renameLayer); buttons.Controls.Add(deleteLayer); buttons.Controls.Add(exitIsolation);
            dialog.Controls.Add(tree); dialog.Controls.Add(breadcrumb); dialog.Controls.Add(buttons); dialog.ShowDialog(this);
        }
    }
    static string LayerCaption(NativeLayer layer)
    {
        return (layer.Hidden ? "[hidden] " : "[visible] ") + (layer.Locked ? "[locked] " : "") + (layer.Targeted ? "[target] " : "") + layer.Name;
    }
    static System.Collections.Generic.IEnumerable<TreeNode> Flatten(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            yield return node;
            foreach (TreeNode child in Flatten(node.Nodes)) yield return child;
        }
    }
    void ExportSvg()
    {
        using (var dialog = new SaveFileDialog { Filter = "SVG files|*.svg", FileName = Path.GetFileNameWithoutExtension(savePath) + ".svg", AddExtension = true, OverwritePrompt = true })
            if (dialog.ShowDialog(this) == DialogResult.OK) File.WriteAllText(dialog.FileName, NativeSceneGraph.ExportSvg(document), new System.Text.UTF8Encoding(false));
    }
    void Undo() { if (undo.Count == 0) return; redo.Push(NativeSceneGraph.Serialize(document)); Restore(undo.Pop()); }
    void Redo() { if (redo.Count == 0) return; undo.Push(NativeSceneGraph.Serialize(document)); Restore(redo.Pop()); }
    void Restore(string text) { NativeDocument restored = NativeSceneGraph.Deserialize(text); document.Nodes.Clear(); document.Nodes.AddRange(restored.Nodes); document.Layers.Clear(); document.Layers.AddRange(restored.Layers); document.Artboard.Width = restored.Artboard.Width; document.Artboard.Height = restored.Artboard.Height; surface.Invalidate(); }
    void CancelDrawing() { drawing = false; penPoints.Clear(); surface.Invalidate(); }
    void SetSelectedHandleMode(string mode)
    {
        NativeNode node = document.Nodes.Find(n => n.Id == selectedId);
        if (node == null || selectedPoint < 0 || selectedPoint >= node.Points.Count) return;
        Snapshot();
        NativeSceneGraph.SetHandleMode(node, selectedPoint, mode);
        node.Path = NativeSceneGraph.PathFor(node.Points.ToArray());
        surface.Invalidate();
    }
    void MoveSelected(double dx, double dy) { NativeNode n = document.Nodes.Find(x => x.Id == selectedId); if (n == null || n.Locked) return; Snapshot(); n.X += dx; n.Y += dy; surface.Invalidate(); }
    void AddPenPath()
    {
        double minX = penPoints.Min(p => p.X), minY = penPoints.Min(p => p.Y), maxX = penPoints.Max(p => p.X), maxY = penPoints.Max(p => p.Y);
        string path = "M" + String.Join("L", penPoints.Select(p => p.X.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + p.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray()) + "Z";
        var points = penPoints.Select(p => new NativePoint { Id = "node-" + Guid.NewGuid().ToString("N"), X = p.X, Y = p.Y, InX = p.X, InY = p.Y, OutX = p.X, OutY = p.Y }).ToArray();
        document.Nodes.Add(new NativeNode { Id = "path-" + Guid.NewGuid().ToString("N"), Kind = "path", Name = "Pen path", X = minX, Y = minY, Width = maxX - minX, Height = maxY - minY, Path = path, Points = points.ToList(), Fill = "#63CDE5" });
        penPoints.Clear();
        surface.Invalidate();
    }
    void RecalculateBounds(NativeNode node)
    {
        if (node.Points == null || node.Points.Count == 0) return;
        node.X = node.Points.Min(p => p.X); node.Y = node.Points.Min(p => p.Y);
        node.Width = node.Points.Max(p => p.X) - node.X; node.Height = node.Points.Max(p => p.Y) - node.Y;
    }
    void BuildInspector()
    {
        inspector.Controls.Clear();
        NativeNode node = document.Nodes.Find(n => n.Id == selectedId);
        if (node == null) return;
        inspector.Controls.Add(new Label { Text = "Transform", ForeColor = Color.White, Dock = DockStyle.Top, Height = 24 });
        var fields = new[] { new { Name = "X", Value = node.X }, new { Name = "Y", Value = node.Y }, new { Name = "W", Value = node.Width }, new { Name = "H", Value = node.Height }, new { Name = "Rotation", Value = node.Rotation } };
        foreach (var field in fields)
        {
            var box = new NumericUpDown { DecimalPlaces = 2, Minimum = -100000, Maximum = 100000, Value = (decimal)field.Value, Dock = DockStyle.Top, Tag = field.Name };
            inspector.Controls.Add(box);
        }
        var apply = new Button { Text = "Apply transform", Dock = DockStyle.Top };
        apply.Click += delegate
        {
            var values = inspector.Controls.OfType<NumericUpDown>().Reverse().ToArray();
            Snapshot();
            NativeSceneGraph.Arrange(document, node.Id, (double)values[0].Value, (double)values[1].Value, (double)values[2].Value, (double)values[3].Value, (double)values[4].Value);
            surface.Invalidate();
        };
        inspector.Controls.Add(apply);
    }
}
