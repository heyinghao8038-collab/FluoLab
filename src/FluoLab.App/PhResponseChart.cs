using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;
using FluoLab.Core;
using FluoLab.Export;

namespace FluoLab.App
{
    // Screen, PNG and SVG share the same complete vector scene.
    internal sealed class PhResponseChart : FrameworkElement
    {
        private readonly List<ChartPrimitive> scene = new List<ChartPrimitive>();
        private readonly List<MeasurementHit> measurementHits = new List<MeasurementHit>();
        private double textScale = 1;
        public event Action<string> PointSelected;
        public const double SceneWidth = 1200;
        public double SceneHeight { get; private set; }
        public PhResponseChart(PhAnalysisResult result, bool synthetic) : this(result, synthetic, false) { }
        public PhResponseChart(PhAnalysisResult result, bool synthetic, bool compact)
        {
            Width = SceneWidth;
            Build(result, synthetic, compact);
            Height = SceneHeight;
            ToolTip = new ToolTip { Content = "悬停原始测量点查看源文件；点击查看原始光谱。" };
        }

        protected override void OnMouseMove(MouseEventArgs args)
        {
            base.OnMouseMove(args);
            var hits = HitMeasurements(args.GetPosition(this));
            var tip = (ToolTip)ToolTip;
            tip.Content = hits.Length == 0 ? "悬停原始测量点查看源文件；点击查看原始光谱。" : string.Join("\n", hits.Select(x =>
                x.Sample.FileName + "\npH=" + x.Sample.Ph.Value.ToString("R", CultureInfo.InvariantCulture) + "；" +
                x.Sample.TargetWavelength.ToString("R", CultureInfo.InvariantCulture) + " nm；原始强度=" + x.Sample.Intensity.Value.ToString("R", CultureInfo.InvariantCulture)));
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs args)
        {
            base.OnMouseLeftButtonUp(args);
            var hits = HitMeasurements(args.GetPosition(this));
            if (hits.Length == 1) { if (PointSelected != null) PointSelected(hits[0].Sample.FileName); }
            else if (hits.Length > 1)
            {
                var menu = new ContextMenu();
                foreach (var hit in hits)
                {
                    var name = hit.Sample.FileName; var item = new MenuItem { Header = name + "  |  " + hit.Sample.Intensity.Value.ToString("R", CultureInfo.InvariantCulture) };
                    item.Click += delegate { if (PointSelected != null) PointSelected(name); }; menu.Items.Add(item);
                }
                menu.IsOpen = true;
            }
        }
        private MeasurementHit[] HitMeasurements(Point point)
        {
            return measurementHits.Where(x => (x.Position - point).Length <= 10).OrderBy(x => (x.Position - point).Length).ToArray();
        }
        private sealed class MeasurementHit
        {
            public Point Position;
            public PhExtractionResult Sample;
        }

        protected override void OnRender(DrawingContext context)
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, SceneWidth, SceneHeight));
            foreach (var item in scene) item.Draw(context);
        }

        public void ExportPng(string path, bool overwrite)
        {
            var image = new DrawingVisual();
            using (var context = image.RenderOpen())
            {
                context.DrawRectangle(Brushes.White, null, new Rect(0, 0, SceneWidth, SceneHeight));
                foreach (var primitive in scene) primitive.Draw(context);
            }
            var bitmap = new RenderTargetBitmap(2400, (int)Math.Ceiling(SceneHeight * 2), 192, 192, PixelFormats.Pbgra32);
            bitmap.Render(image);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            AtomicOutput.Write(path, overwrite, delegate(string temporary)
            {
                using (var stream = File.Create(temporary)) encoder.Save(stream);
            });
        }

        public void ExportSvg(string path, bool overwrite)
        {
            AtomicOutput.Write(path, overwrite, delegate(string temporary)
            {
                using (var writer = XmlWriter.Create(temporary, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
                {
                    writer.WriteStartElement("svg", "http://www.w3.org/2000/svg");
                    writer.WriteAttributeString("width", SceneWidth.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("height", SceneHeight.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("viewBox", "0 0 " + SceneWidth + " " + SceneHeight.ToString(CultureInfo.InvariantCulture));
                    writer.WriteStartElement("title"); writer.WriteString("pH 响应描述统计"); writer.WriteEndElement();
                    writer.WriteStartElement("rect");
                    writer.WriteAttributeString("width", "100%"); writer.WriteAttributeString("height", "100%"); writer.WriteAttributeString("fill", "white"); writer.WriteEndElement();
                    foreach (var primitive in scene) primitive.WriteSvg(writer);
                    writer.WriteEndElement();
                }
            });
        }

        private void Build(PhAnalysisResult result, bool synthetic, bool compact)
        {
            textScale = compact ? 1.4 : 1;
            AddText(synthetic ? "pH 响应分析（模拟数据）" : "pH 响应分析", 36, 24, 24, "#1D1D1F", true);
            var wavelength = result.TargetWavelength.ToString("R", CultureInfo.InvariantCulture);
            var subtitleHeight = AddWrapped("目标波长 " + wavelength + " nm；原始测量点 + 组均值（菱形）；误差棒 = 均值 ± 样本 SD（n≥2）", 36, 65, 1128, 15, "#55555B");
            var yTitle = "荧光强度" + (string.IsNullOrWhiteSpace(result.IntensityUnit) ? "" : " (" + result.IntensityUnit + ")");
            var titleTop = Math.Max(102, 65 + subtitleHeight + 8);
            var titleHeight = AddWrapped(yTitle, 36, titleTop, 1100, 15, "#1D1D1F");
            const double left = 150, right = 1150;
            var top = Math.Max(compact ? 148 : 132, titleTop + titleHeight + 14);
            var bottom = top + (compact ? 238.0 : 443.0);
            var samples = result.Samples.Where(x => x.Status == ExtractionStatus.Success).ToArray();
            var xScale = Math.Max(1, result.Groups.Max(x => Math.Abs(x.Ph)));
            var xMin = result.Groups.Min(x => x.Ph / xScale);
            var xMax = result.Groups.Max(x => x.Ph / xScale);
            var xPad = xMax == xMin ? Math.Max(0.05, 1.0 / xScale) : (xMax - xMin) * 0.05;
            if (xPad == 0) xPad = 0.1;
            xMin -= xPad; xMax += xPad;
            var yScale = Math.Max(1, samples.Max(x => Math.Abs(x.Intensity.Value)));
            yScale = Math.Max(yScale, result.Groups.Max(x => x.Sd ?? 0));
            var yMin = Math.Min(samples.Min(x => x.Intensity.Value / yScale), result.Groups.Min(x => x.Mean / yScale - (x.Sd ?? 0) / yScale));
            var yMax = Math.Max(samples.Max(x => x.Intensity.Value / yScale), result.Groups.Max(x => x.Mean / yScale + (x.Sd ?? 0) / yScale));
            var yPad = yMax == yMin ? Math.Max(1.0 / yScale, Math.Abs(yMax) * 0.05) : (yMax - yMin) * 0.1;
            yMin -= yPad; yMax += yPad;
            Func<double, double> screenX = value => left + (value / xScale - xMin) / (xMax - xMin) * (right - left);
            Func<double, double> screenYScaled = value => bottom - (value - yMin) / (yMax - yMin) * (bottom - top);
            for (var tick = 0; tick <= 5; tick++)
            {
                var value = yMin + (yMax - yMin) * tick / 5;
                var y = screenYScaled(value);
                Line(left, y, right, y, "#E8E8ED", 1);
                AddText(FormatScaled(value, yScale), left - 12, y - 10, 13, "#55555B", false, "end");
            }
            var phValues = result.Groups.Select(x => x.Ph).Distinct().OrderBy(x => x).ToArray();
            var tickValues = phValues.Length <= 12 ? phValues : Enumerable.Range(0, 9)
                .Select(i => phValues[(int)Math.Round(i * (phValues.Length - 1) / 8.0)]).Distinct().ToArray();
            var lastLabelRight = double.NegativeInfinity;
            foreach (var ph in tickValues)
            {
                var x = screenX(ph);
                Line(x, top, x, bottom, "#F0F0F3", 1);
                var label = ph.ToString("G8", CultureInfo.InvariantCulture);
                var halfWidth = Measure(label, 13 * textScale, false).Width / 2;
                if (x - halfWidth >= lastLabelRight + 8)
                { AddText(label, x, bottom + 12, 13, "#55555B", false, "middle"); lastLabelRight = x + halfWidth; }
            }
            Line(left, bottom, right, bottom, "#9C9CA3", 1.2);
            Line(left, top, left, bottom, "#9C9CA3", 1.2);
            AddText("pH", (left + right) / 2, bottom + 44, 17, "#1D1D1F", true, "middle");
            var series = result.Groups.Select(x => x.Series).Distinct().ToArray();
            var legendY = bottom + 75;
            for (var seriesIndex = 0; seriesIndex < series.Length; seriesIndex++)
            {
                var name = series[seriesIndex];
                var color = Palette(seriesIndex);
                foreach (var sample in samples.Where(x => x.Series == name))
                {
                    Symbol(screenX(sample.Ph.Value), screenYScaled(sample.Intensity.Value / yScale), 6, color, seriesIndex % 3, false);
                    measurementHits.Add(new MeasurementHit { Position = new Point(screenX(sample.Ph.Value), screenYScaled(sample.Intensity.Value / yScale)), Sample = sample });
                }
                foreach (var group in result.Groups.Where(x => x.Series == name))
                {
                    var x = screenX(group.Ph);
                    if (group.Sd.HasValue)
                    {
                        var low = screenYScaled(group.Mean / yScale - group.Sd.Value / yScale);
                        var high = screenYScaled(group.Mean / yScale + group.Sd.Value / yScale);
                        Line(x, low, x, high, color, 2);
                        Line(x - 8, low, x + 8, low, color, 2);
                        Line(x - 8, high, x + 8, high, color, 2);
                    }
                    Symbol(x, screenYScaled(group.Mean / yScale), 4.5, color, 3, true);
                }
                Symbol(48, legendY + 9, 6, color, seriesIndex % 3, false);
                var labelHeight = AddWrapped(name + "（" + PhAnalysisService.RepeatLabel(result.Groups.First(x => x.Series == name).RepeatType) + "）", 66, legendY, 1080, 15, color);
                legendY += labelHeight + 6;
                var countText = "测量次数：" + string.Join("；", result.Groups.Where(x => x.Series == name).Select(x => "pH " + x.Ph.ToString("R", CultureInfo.InvariantCulture) + "：n=" + x.N + (x.N == 1 ? "，SD 不可计算" : "")));
                legendY += AddWrapped(countText, 66, legendY, 1080, 13, "#55555B") + 18;
            }
            legendY += AddWrapped("仅描述统计，不进行拟合或显著性检验。原始点按真实 pH 位置绘制；重合点可能叠加。", 36, legendY, 1128, 13, "#55555B") + 12;
            SceneHeight = Math.Ceiling(legendY + 22);
        }

        private void Symbol(double x, double y, double radius, string color, int shape, bool filled)
        {
            if (shape == 0) scene.Add(new ChartPrimitive { Kind = "circle", X = x, Y = y, Radius = radius, Color = color, Filled = filled, Weight = 1.6 });
            else
            {
                Point[] points = shape == 1 ? new[] { new Point(x - radius, y - radius), new Point(x + radius, y - radius), new Point(x + radius, y + radius), new Point(x - radius, y + radius) }
                    : shape == 2 ? new[] { new Point(x, y - radius), new Point(x + radius, y + radius), new Point(x - radius, y + radius) }
                    : new[] { new Point(x, y - radius), new Point(x + radius, y), new Point(x, y + radius), new Point(x - radius, y) };
                scene.Add(new ChartPrimitive { Kind = "polygon", Points = points, Color = color, Filled = filled, Weight = 1.6 });
            }
        }
        private void Line(double x, double y, double x2, double y2, string color, double weight)
        { scene.Add(new ChartPrimitive { Kind = "line", X = x, Y = y, X2 = x2, Y2 = y2, Color = color, Weight = weight }); }
        private void AddText(string text, double x, double y, double size, string color, bool bold, string anchor = "start")
        { scene.Add(new ChartPrimitive { Kind = "text", Text = text, X = x, Y = y, Size = size * textScale, Color = color, Bold = bold, Anchor = anchor }); }
        private double AddWrapped(string text, double x, double y, double width, double size, string color)
        {
            var line = "";
            var row = 0;
            foreach (var character in text)
            {
                var candidate = line + character;
                if (Measure(candidate, size * textScale, false).Width > width && line.Length > 0)
                { AddText(line, x, y + row * (size * textScale + 7), size, color, false); row++; line = character.ToString(); }
                else line = candidate;
            }
            AddText(line, x, y + row * (size * textScale + 7), size, color, false);
            return (row + 1) * (size * textScale + 7);
        }
        private static string Format(double value)
        { return value.ToString(Math.Abs(value) >= 100000 || (value != 0 && Math.Abs(value) < 0.001) ? "0.###E+0" : "0.####", CultureInfo.InvariantCulture); }
        private static string FormatScaled(double value, double scale)
        {
            var product = value * scale;
            if (!double.IsInfinity(product)) return Format(product);
            var exponent = (int)Math.Floor(Math.Log10(scale));
            var mantissa = value * (scale / Math.Pow(10, exponent));
            while (Math.Abs(mantissa) >= 10) { mantissa /= 10; exponent++; }
            return mantissa.ToString("0.###", CultureInfo.InvariantCulture) + "E+" + exponent;
        }
        private static string Palette(int index)
        {
            var colors = new[] { "#0071E3", "#CE4D38", "#248450", "#8B55AD", "#B47A0B", "#008C92", "#C54883", "#5959A8" };
            if (index < colors.Length) return colors[index];
            var angle = (index * 137.508) % 360;
            var hue = angle / 60;
            var chroma = 0.65;
            var secondary = chroma * (1 - Math.Abs(hue % 2 - 1));
            double r = 0, g = 0, b = 0;
            if (hue < 1) { r = chroma; g = secondary; }
            else if (hue < 2) { r = secondary; g = chroma; }
            else if (hue < 3) { g = chroma; b = secondary; }
            else if (hue < 4) { g = secondary; b = chroma; }
            else if (hue < 5) { r = secondary; b = chroma; }
            else { r = chroma; b = secondary; }
            return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", (int)((r + 0.1) * 255), (int)((g + 0.1) * 255), (int)((b + 0.1) * 255));
        }
        internal static FormattedText Measure(string text, double size, bool bold)
        {
#pragma warning disable 618
            return new FormattedText(text, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Microsoft YaHei, Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, Brushes.Black);
#pragma warning restore 618
        }

        private sealed class ChartPrimitive
        {
            public string Kind, Text, Color, Anchor;
            public double X, Y, X2, Y2, Radius, Size, Weight;
            public bool Bold, Filled;
            public Point[] Points;
            public void Draw(DrawingContext context)
            {
                var brush = (Brush)new BrushConverter().ConvertFromString(Color);
                var pen = new Pen(brush, Weight);
                if (Kind == "line") context.DrawLine(pen, new Point(X, Y), new Point(X2, Y2));
                else if (Kind == "circle") context.DrawEllipse(Filled ? brush : Brushes.Transparent, pen, new Point(X, Y), Radius, Radius);
                else if (Kind == "polygon")
                {
                    var geometry = new StreamGeometry();
                    using (var part = geometry.Open())
                    { part.BeginFigure(Points[0], Filled, true); part.PolyLineTo(Points.Skip(1).ToArray(), true, false); }
                    context.DrawGeometry(Filled ? brush : Brushes.Transparent, pen, geometry);
                }
                else
                {
                    var formatted = Measure(Text, Size, Bold); formatted.SetForegroundBrush(brush);
                    var x = Anchor == "end" ? X - formatted.Width : Anchor == "middle" ? X - formatted.Width / 2 : X;
                    context.DrawText(formatted, new Point(x, Y));
                }
            }
            public void WriteSvg(XmlWriter writer)
            {
                writer.WriteStartElement(Kind);
                if (Kind == "line") { Attribute(writer, "x1", X); Attribute(writer, "y1", Y); Attribute(writer, "x2", X2); Attribute(writer, "y2", Y2); }
                else if (Kind == "circle") { Attribute(writer, "cx", X); Attribute(writer, "cy", Y); Attribute(writer, "r", Radius); }
                else if (Kind == "polygon") writer.WriteAttributeString("points", string.Join(" ", Points.Select(p => p.X.ToString("R", CultureInfo.InvariantCulture) + "," + p.Y.ToString("R", CultureInfo.InvariantCulture))));
                else
                {
                    Attribute(writer, "x", X); Attribute(writer, "y", Y + Size);
                    writer.WriteAttributeString("font-family", "Microsoft YaHei, Noto Sans CJK SC, sans-serif");
                    Attribute(writer, "font-size", Size);
                    writer.WriteAttributeString("text-anchor", Anchor ?? "start");
                    writer.WriteAttributeString("font-weight", Bold ? "600" : "400");
                    writer.WriteAttributeString("fill", Color); writer.WriteString(Text); writer.WriteEndElement(); return;
                }
                writer.WriteAttributeString("stroke", Color); Attribute(writer, "stroke-width", Weight);
                writer.WriteAttributeString("fill", Filled ? Color : "none"); writer.WriteEndElement();
            }
            private static void Attribute(XmlWriter writer, string name, double value)
            { writer.WriteAttributeString(name, value.ToString("R", CultureInfo.InvariantCulture)); }
        }
    }
}
