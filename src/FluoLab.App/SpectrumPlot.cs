using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FluoLab.Core;

namespace FluoLab.App
{
    internal sealed class PlotSeries
    {
        public PlotSeries(SpectrumDataset dataset, Brush stroke)
        {
            Dataset = dataset;
            Stroke = stroke;
        }

        public SpectrumDataset Dataset { get; private set; }
        public Brush Stroke { get; private set; }
    }

    internal sealed class SpectrumPlot : FrameworkElement
    {
        private readonly List<PlotSeries> series = new List<PlotSeries>();
        private readonly ToolTip hoverTip;
        private Rect plotArea;
        private double dataMinX;
        private double dataMaxX;
        private double dataMinY;
        private double dataMaxY;
        private double viewMinX;
        private double viewMaxX;
        private double viewMinY;
        private double viewMaxY;
        private bool hasView;
        private bool isPanning;
        private Point panStart;
        private double panMinX;
        private double panMaxX;
        private double panMinY;
        private double panMaxY;

        public SpectrumPlot()
        {
            Focusable = true;
            ClipToBounds = true;
            Background = Brushes.White;
            hoverTip = new ToolTip
            {
                Placement = PlacementMode.Mouse,
                StaysOpen = true,
                Background = new SolidColorBrush(Color.FromArgb(245, 29, 29, 31)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 12
            };
            ToolTip = hoverTip;
        }

        public Brush Background { get; set; }

        public void SetSeries(IEnumerable<PlotSeries> newSeries)
        {
            series.Clear();
            if (newSeries != null)
            {
                series.AddRange(newSeries.Where(item => item != null && item.Dataset != null));
            }

            ResetView();
        }

        public void ResetView()
        {
            hasView = false;
            hoverTip.IsOpen = false;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            drawingContext.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));

            plotArea = new Rect(64, 18, Math.Max(1, ActualWidth - 84), Math.Max(1, ActualHeight - 66));
            if (series.Count == 0)
            {
                DrawEmptyState(drawingContext);
                return;
            }

            CalculateDataBounds();
            if (!hasView)
            {
                ApplyDataView();
            }

            DrawGridAndAxes(drawingContext);
            foreach (var item in series)
            {
                DrawSeries(drawingContext, item);
            }

            DrawAxisTitles(drawingContext);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs eventArgs)
        {
            base.OnMouseWheel(eventArgs);
            if (series.Count == 0 || !plotArea.Contains(eventArgs.GetPosition(this)))
            {
                return;
            }

            var position = eventArgs.GetPosition(this);
            var anchorX = ScreenToDataX(position.X);
            var anchorY = ScreenToDataY(position.Y);
            var factor = eventArgs.Delta > 0 ? 0.82 : 1.22;
            viewMinX = anchorX + (viewMinX - anchorX) * factor;
            viewMaxX = anchorX + (viewMaxX - anchorX) * factor;
            viewMinY = anchorY + (viewMinY - anchorY) * factor;
            viewMaxY = anchorY + (viewMaxY - anchorY) * factor;
            ClampView();
            hasView = true;
            InvalidateVisual();
            eventArgs.Handled = true;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs eventArgs)
        {
            base.OnMouseLeftButtonDown(eventArgs);
            if (eventArgs.ClickCount == 2)
            {
                ResetView();
                eventArgs.Handled = true;
                return;
            }

            if (series.Count == 0 || !plotArea.Contains(eventArgs.GetPosition(this)))
            {
                return;
            }

            Focus();
            isPanning = true;
            panStart = eventArgs.GetPosition(this);
            panMinX = viewMinX;
            panMaxX = viewMaxX;
            panMinY = viewMinY;
            panMaxY = viewMaxY;
            CaptureMouse();
            Cursor = Cursors.SizeAll;
            hoverTip.IsOpen = false;
            eventArgs.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs eventArgs)
        {
            base.OnMouseLeftButtonUp(eventArgs);
            if (!isPanning)
            {
                return;
            }

            isPanning = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
            eventArgs.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs eventArgs)
        {
            base.OnMouseMove(eventArgs);
            var position = eventArgs.GetPosition(this);
            if (isPanning)
            {
                var deltaX = (position.X - panStart.X) / plotArea.Width * (panMaxX - panMinX);
                var deltaY = (position.Y - panStart.Y) / plotArea.Height * (panMaxY - panMinY);
                viewMinX = panMinX - deltaX;
                viewMaxX = panMaxX - deltaX;
                viewMinY = panMinY + deltaY;
                viewMaxY = panMaxY + deltaY;
                ClampView();
                hasView = true;
                InvalidateVisual();
                return;
            }

            UpdateHover(position);
        }

        protected override void OnMouseLeave(MouseEventArgs eventArgs)
        {
            base.OnMouseLeave(eventArgs);
            if (!isPanning)
            {
                hoverTip.IsOpen = false;
            }
        }

        private void DrawEmptyState(DrawingContext drawingContext)
        {
            var title = CreateText("选择左侧文件以预览曲线", 16, FontWeights.SemiBold, Theme.PrimaryTextBrush);
            var subtitle = CreateText("勾选只影响预览，导出始终包含全部有效文件", 12, FontWeights.Normal, Theme.SecondaryTextBrush);
            drawingContext.DrawText(title, new Point((ActualWidth - title.Width) / 2, (ActualHeight - 48) / 2));
            drawingContext.DrawText(subtitle, new Point((ActualWidth - subtitle.Width) / 2, (ActualHeight + 4) / 2));
        }

        private void CalculateDataBounds()
        {
            dataMinX = double.PositiveInfinity;
            dataMaxX = double.NegativeInfinity;
            dataMinY = double.PositiveInfinity;
            dataMaxY = double.NegativeInfinity;

            foreach (var item in series)
            {
                var dataset = item.Dataset;
                dataMinX = Math.Min(dataMinX, dataset.Wavelengths[0]);
                dataMaxX = Math.Max(dataMaxX, dataset.Wavelengths[dataset.PointCount - 1]);
                for (var index = 0; index < dataset.Intensities.Length; index++)
                {
                    dataMinY = Math.Min(dataMinY, dataset.Intensities[index]);
                    dataMaxY = Math.Max(dataMaxY, dataset.Intensities[index]);
                }
            }

            if (dataMaxX <= dataMinX)
            {
                dataMaxX = dataMinX + 1;
            }

            if (dataMaxY <= dataMinY)
            {
                dataMaxY = dataMinY + 1;
            }
        }

        private void ApplyDataView()
        {
            var yPadding = (dataMaxY - dataMinY) * 0.06;
            viewMinX = dataMinX;
            viewMaxX = dataMaxX;
            viewMinY = dataMinY - yPadding;
            viewMaxY = dataMaxY + yPadding;
            hasView = true;
        }

        private void ClampView()
        {
            var minimumXSpan = Math.Max((dataMaxX - dataMinX) / 10000.0, 0.000001);
            var minimumYSpan = Math.Max((dataMaxY - dataMinY) / 10000.0, 0.000001);
            if (viewMaxX - viewMinX < minimumXSpan)
            {
                var center = (viewMaxX + viewMinX) / 2;
                viewMinX = center - minimumXSpan / 2;
                viewMaxX = center + minimumXSpan / 2;
            }

            if (viewMaxY - viewMinY < minimumYSpan)
            {
                var center = (viewMaxY + viewMinY) / 2;
                viewMinY = center - minimumYSpan / 2;
                viewMaxY = center + minimumYSpan / 2;
            }
        }

        private void DrawGridAndAxes(DrawingContext drawingContext)
        {
            var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(0xEA, 0xEA, 0xED)), 1);
            var axisPen = new Pen(Theme.DividerBrush, 1);
            gridPen.Freeze();
            axisPen.Freeze();

            var ySteps = Math.Max(1, Math.Min(5, (int)(plotArea.Height / 28)));
            for (var step = 0; step <= 5; step++)
            {
                var x = plotArea.Left + plotArea.Width * step / 5.0;
                var y = plotArea.Top + plotArea.Height * step / ySteps;
                drawingContext.DrawLine(gridPen, new Point(x, plotArea.Top), new Point(x, plotArea.Bottom));
                if (step <= ySteps) drawingContext.DrawLine(gridPen, new Point(plotArea.Left, y), new Point(plotArea.Right, y));

                var xValue = viewMinX + (viewMaxX - viewMinX) * step / 5.0;
                var yValue = viewMaxY - (viewMaxY - viewMinY) * step / ySteps;
                var xLabel = CreateText(FormatAxisValue(xValue), 10, FontWeights.Normal, Theme.SecondaryTextBrush);
                var yLabel = CreateText(FormatAxisValue(yValue), 10, FontWeights.Normal, Theme.SecondaryTextBrush);
                drawingContext.DrawText(xLabel, new Point(x - xLabel.Width / 2, plotArea.Bottom + 7));
                if (step <= ySteps) drawingContext.DrawText(yLabel, new Point(plotArea.Left - yLabel.Width - 9, y - yLabel.Height / 2));
            }

            drawingContext.DrawRectangle(null, axisPen, plotArea);
        }

        private void DrawSeries(DrawingContext drawingContext, PlotSeries item)
        {
            var dataset = item.Dataset;
            var visibleStart = FindNearestIndex(dataset.Wavelengths, viewMinX);
            var visibleEnd = FindNearestIndex(dataset.Wavelengths, viewMaxX);
            if (visibleEnd < visibleStart)
            {
                var swap = visibleStart;
                visibleStart = visibleEnd;
                visibleEnd = swap;
            }

            visibleStart = Math.Max(0, visibleStart - 1);
            visibleEnd = Math.Min(dataset.PointCount - 1, visibleEnd + 1);
            if (visibleEnd <= visibleStart)
            {
                return;
            }

            var points = BuildDisplayPoints(dataset, visibleStart, visibleEnd, Math.Max(100, (int)plotArea.Width * 2));
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                var first = true;
                foreach (var pointIndex in points)
                {
                    var point = new Point(
                        DataToScreenX(dataset.Wavelengths[pointIndex]),
                        DataToScreenY(dataset.Intensities[pointIndex]));
                    if (first)
                    {
                        context.BeginFigure(point, false, false);
                        first = false;
                    }
                    else
                    {
                        context.LineTo(point, true, false);
                    }
                }
            }

            geometry.Freeze();
            var pen = new Pen(item.Stroke, 1.35);
            pen.Freeze();
            drawingContext.PushClip(new RectangleGeometry(plotArea));
            drawingContext.DrawGeometry(null, pen, geometry);
            drawingContext.Pop();
        }

        private static IEnumerable<int> BuildDisplayPoints(
            SpectrumDataset dataset,
            int start,
            int end,
            int budget)
        {
            var count = end - start + 1;
            if (count <= budget)
            {
                for (var index = start; index <= end; index++)
                {
                    yield return index;
                }

                yield break;
            }

            var bucketSize = Math.Max(1, (int)Math.Ceiling(count / (budget / 2.0)));
            yield return start;
            for (var bucketStart = start + 1; bucketStart < end; bucketStart += bucketSize)
            {
                var bucketEnd = Math.Min(end - 1, bucketStart + bucketSize - 1);
                var minIndex = bucketStart;
                var maxIndex = bucketStart;
                for (var index = bucketStart + 1; index <= bucketEnd; index++)
                {
                    if (dataset.Intensities[index] < dataset.Intensities[minIndex])
                    {
                        minIndex = index;
                    }

                    if (dataset.Intensities[index] > dataset.Intensities[maxIndex])
                    {
                        maxIndex = index;
                    }
                }

                if (minIndex < maxIndex)
                {
                    yield return minIndex;
                    yield return maxIndex;
                }
                else if (maxIndex < minIndex)
                {
                    yield return maxIndex;
                    yield return minIndex;
                }
                else
                {
                    yield return minIndex;
                }
            }

            yield return end;
        }

        private void DrawAxisTitles(DrawingContext drawingContext)
        {
            var xTitle = CreateText("波长 (nm)", 11, FontWeights.SemiBold, Theme.SecondaryTextBrush);
            drawingContext.DrawText(xTitle, new Point(plotArea.Left + (plotArea.Width - xTitle.Width) / 2, ActualHeight - 18));
            var yTitle = CreateText("荧光强度", 11, FontWeights.SemiBold, Theme.SecondaryTextBrush);
            drawingContext.PushTransform(new RotateTransform(-90));
            drawingContext.DrawText(yTitle, new Point(-(plotArea.Top + (plotArea.Height + yTitle.Width) / 2), 8));
            drawingContext.Pop();
        }

        private void UpdateHover(Point position)
        {
            if (series.Count == 0 || !plotArea.Contains(position))
            {
                hoverTip.IsOpen = false;
                return;
            }

            var targetWavelength = ScreenToDataX(position.X);
            PlotSeries bestSeries = null;
            var bestIndex = -1;
            var bestDistance = double.PositiveInfinity;

            foreach (var item in series)
            {
                var index = FindNearestIndex(item.Dataset.Wavelengths, targetWavelength);
                var x = DataToScreenX(item.Dataset.Wavelengths[index]);
                var y = DataToScreenY(item.Dataset.Intensities[index]);
                var distance = Math.Sqrt(Math.Pow(x - position.X, 2) + Math.Pow(y - position.Y, 2));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestSeries = item;
                    bestIndex = index;
                }
            }

            if (bestSeries == null || bestIndex < 0 || bestDistance > 40)
            {
                hoverTip.IsOpen = false;
                return;
            }

            var dataset = bestSeries.Dataset;
            hoverTip.Content = string.Format(
                CultureInfo.CurrentCulture,
                "{0}\n波长：{1:R} nm\n荧光强度：{2:R}",
                dataset.DisplayName,
                dataset.Wavelengths[bestIndex],
                dataset.Intensities[bestIndex]);
            hoverTip.IsOpen = true;
        }

        private double DataToScreenX(double value)
        {
            return plotArea.Left + (value - viewMinX) / (viewMaxX - viewMinX) * plotArea.Width;
        }

        private double DataToScreenY(double value)
        {
            return plotArea.Bottom - (value - viewMinY) / (viewMaxY - viewMinY) * plotArea.Height;
        }

        private double ScreenToDataX(double value)
        {
            return viewMinX + (value - plotArea.Left) / plotArea.Width * (viewMaxX - viewMinX);
        }

        private double ScreenToDataY(double value)
        {
            return viewMinY + (plotArea.Bottom - value) / plotArea.Height * (viewMaxY - viewMinY);
        }

        private static int FindNearestIndex(double[] values, double target)
        {
            var index = Array.BinarySearch(values, target);
            if (index >= 0)
            {
                return index;
            }

            var upper = ~index;
            if (upper <= 0)
            {
                return 0;
            }

            if (upper >= values.Length)
            {
                return values.Length - 1;
            }

            return Math.Abs(values[upper] - target) < Math.Abs(values[upper - 1] - target)
                ? upper
                : upper - 1;
        }

        private static string FormatAxisValue(double value)
        {
            var absolute = Math.Abs(value);
            if ((absolute > 0 && absolute < 0.01) || absolute >= 100000)
            {
                return value.ToString("0.##E+0", CultureInfo.CurrentCulture);
            }

            return value.ToString("0.##", CultureInfo.CurrentCulture);
        }

        private static FormattedText CreateText(
            string text,
            double size,
            FontWeight weight,
            Brush brush)
        {
#pragma warning disable 618
            return new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI, Microsoft YaHei UI"), FontStyles.Normal, weight, FontStretches.Normal),
                size,
                brush);
#pragma warning restore 618
        }
    }
}

