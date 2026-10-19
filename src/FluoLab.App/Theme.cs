using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace FluoLab.App
{
    internal static class Theme
    {
        public static readonly Color AccentColor = Color.FromRgb(0x00, 0x71, 0xE3);
        public static readonly Brush AccentBrush = Freeze(new SolidColorBrush(AccentColor));
        public static readonly Brush BackgroundBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF7)));
        public static readonly Brush SurfaceBrush = Freeze(new SolidColorBrush(Colors.White));
        public static readonly Brush SecondarySurfaceBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF4)));
        public static readonly Brush PrimaryTextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x1D, 0x1D, 0x1F)));
        public static readonly Brush SecondaryTextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x73)));
        public static readonly Brush DividerBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD2, 0xD2, 0xD7)));
        public static readonly Brush SuccessBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x2E, 0x9E, 0x5B)));
        public static readonly Brush WarningBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xB2, 0x6A, 0x00)));
        public static readonly Brush ErrorBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0x2D, 0x20)));

        public static ResourceDictionary CreateResources()
        {
            WindowPlacement.Register();
            const string xaml = @"
<ResourceDictionary
  xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
  xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
  <FontFamily x:Key=""AppFont"">Segoe UI Variable, Microsoft YaHei UI, Segoe UI</FontFamily>

  <Style TargetType=""Window"">
    <Setter Property=""WindowStyle"" Value=""SingleBorderWindow""/>
    <Setter Property=""ResizeMode"" Value=""CanResize""/>
    <Setter Property=""FontFamily"" Value=""{StaticResource AppFont}""/>
    <Setter Property=""Foreground"" Value=""#1D1D1F""/>
    <Setter Property=""Background"" Value=""#F5F5F7""/>
  </Style>

  <Style TargetType=""Button"">
    <Setter Property=""FontFamily"" Value=""{StaticResource AppFont}""/>
    <Setter Property=""FontSize"" Value=""13""/>
    <Setter Property=""FontWeight"" Value=""SemiBold""/>
    <Setter Property=""Padding"" Value=""16,9""/>
    <Setter Property=""MinHeight"" Value=""38""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Background"" Value=""#F2F2F4""/>
    <Setter Property=""Foreground"" Value=""#1D1D1F""/>
    <Setter Property=""BorderBrush"" Value=""#D2D2D7""/>
    <Setter Property=""BorderThickness"" Value=""1""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""Border"" CornerRadius=""10"" Background=""{TemplateBinding Background}""
                  BorderBrush=""{TemplateBinding BorderBrush}"" BorderThickness=""{TemplateBinding BorderThickness}""
                  Padding=""{TemplateBinding Padding}"">
            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""Border"" Property=""Opacity"" Value=""0.86""/>
            </Trigger>
            <Trigger Property=""IsPressed"" Value=""True"">
              <Setter TargetName=""Border"" Property=""Opacity"" Value=""0.72""/>
            </Trigger>
            <Trigger Property=""IsEnabled"" Value=""False"">
              <Setter TargetName=""Border"" Property=""Opacity"" Value=""0.42""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key=""PrimaryButton"" TargetType=""Button"" BasedOn=""{StaticResource {x:Type Button}}"">
    <Setter Property=""Background"" Value=""#0071E3""/>
    <Setter Property=""Foreground"" Value=""White""/>
    <Setter Property=""BorderBrush"" Value=""#0071E3""/>
  </Style>

  <Style x:Key=""QuietButton"" TargetType=""Button"" BasedOn=""{StaticResource {x:Type Button}}"">
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""BorderBrush"" Value=""Transparent""/>
    <Setter Property=""Padding"" Value=""10,6""/>
    <Setter Property=""MinHeight"" Value=""32""/>
  </Style>

  <Style x:Key=""QuickActionButton"" TargetType=""Button"" BasedOn=""{StaticResource {x:Type Button}}"">
    <Setter Property=""Background"" Value=""White""/>
    <Setter Property=""Foreground"" Value=""#0071E3""/>
    <Setter Property=""BorderBrush"" Value=""#87B9EF""/>
    <Setter Property=""BorderThickness"" Value=""1""/>
    <Setter Property=""FontSize"" Value=""14""/>
    <Setter Property=""MinHeight"" Value=""44""/>
    <Setter Property=""Padding"" Value=""16,10""/>
    <Setter Property=""Margin"" Value=""0,3,10,3""/>
  </Style>

  <Style TargetType=""TextBox"">
    <Setter Property=""FontFamily"" Value=""{StaticResource AppFont}""/>
    <Setter Property=""FontSize"" Value=""13""/>
    <Setter Property=""Padding"" Value=""10,8""/>
    <Setter Property=""BorderBrush"" Value=""#D2D2D7""/>
    <Setter Property=""Background"" Value=""White""/>
  </Style>

  <Style TargetType=""TabControl"">
    <Setter Property=""Background"" Value=""White""/>
    <Setter Property=""BorderBrush"" Value=""#D2D2D7""/>
    <Setter Property=""Padding"" Value=""8""/>
  </Style>
  <Style TargetType=""TabItem"">
    <Setter Property=""FontSize"" Value=""13""/>
    <Setter Property=""Margin"" Value=""0,0,6,6""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""TabItem"">
          <Border x:Name=""Tab"" Background=""#F2F2F4"" CornerRadius=""8"" Padding=""14,8"">
            <ContentPresenter x:Name=""Header"" ContentSource=""Header"" TextElement.Foreground=""#1D1D1F""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsSelected"" Value=""True"">
              <Setter TargetName=""Tab"" Property=""Background"" Value=""#0071E3""/>
              <Setter TargetName=""Header"" Property=""TextElement.Foreground"" Value=""White""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""CheckBox"">
    <Setter Property=""FontFamily"" Value=""{StaticResource AppFont}""/>
    <Setter Property=""FontSize"" Value=""13""/>
    <Setter Property=""VerticalContentAlignment"" Value=""Center""/>
  </Style>
</ResourceDictionary>";

            return (ResourceDictionary)XamlReader.Parse(xaml);
        }

        private static Brush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}

