# Client UI Redesign ("Military Utility") Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle and restructure the IL2-SRS client main window into a compact, modern, military-utility themed UI (5 tabs + persistent status bar) without changing any functionality.

**Architecture:** Pure XAML restyle on existing MahApps.Metro 1.5 / .NET Framework 4.8.1. New resource dictionaries (`MilitaryPalette.xaml` app-wide for brushes, `MilitaryControls.xaml` merged **into MainWindow.Resources only** so the radio overlay and popups are not affected by implicit styles — this is a deliberate refinement of the spec, which proposed App.xaml-level merge; the overlay has bare controls that would otherwise change). The legacy `ClientThemeManager` visual-tree-walking theme system is deleted. All moved controls keep their `x:Name`, bindings, `Click` handlers, and English text (= localization keys) verbatim.

**Tech Stack:** WPF (.NET Framework 4.8.1), MahApps.Metro 1.5.0, old-style .csproj (every new file MUST be registered in `IL2-SR-Client/IL2-SR-Client.csproj`), resx-based localization keyed by English text via `LocalizationManager.LocalizeElement`.

**Spec:** `docs/superpowers/specs/2026-06-11-client-ui-redesign-design.md`

**Testing note:** This is a XAML restyle with no unit-testable logic (the two new converters are 5-line pure functions; the existing test project `IL2-SR-CommonTests` references only `IL2-SR-Common`, not the client). Verification per task = build succeeds + app launches + visual/functional check. This is the agreed adaptation of TDD for this plan.

**Build commands used throughout** (PowerShell, from repo root):

```powershell
$msbuild = & "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
& $msbuild "IL2-SR-Client\IL2-SR-Client.csproj" /p:Configuration=Debug /v:m /nologo
```

If `vswhere.exe` is missing, locate MSBuild with `Get-Command msbuild` or check `C:\Program Files\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\MSBuild.exe`.

Launch for visual checks:

```powershell
& "IL2-SR-Client\bin\Debug\IL2-SR-ClientRadio.exe"
```

(If the output exe name differs, check `<AssemblyName>` in the csproj — the pack URIs in this plan use `IL2-SR-ClientRadio` because `PilotRosterWindow.xaml:23` already references that assembly name; verify it matches.)

---

### Task 0: Baseline build

Confirm the project builds BEFORE any changes, so later failures are attributable.

**Files:** none modified.

- [ ] **Step 0.1: Restore NuGet packages**

```powershell
nuget restore IL2-SimpleRadioStandalone.sln
```

If `nuget` is not on PATH: `Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile nuget.exe` then `.\nuget.exe restore IL2-SimpleRadioStandalone.sln`.

Expected: `packages/` directory populated (MahApps.Metro.1.5.0 etc.).

- [ ] **Step 0.2: Build the client project**

```powershell
$msbuild = & "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
& $msbuild "IL2-SR-Client\IL2-SR-Client.csproj" /p:Configuration=Debug /v:m /nologo
```

Expected: `Build succeeded. 0 Error(s)`. Warnings are acceptable. If the baseline build fails, STOP and report — do not proceed.

- [ ] **Step 0.3: Launch the app once and screenshot the current look** (reference for comparison)

```powershell
& "IL2-SR-Client\bin\Debug\IL2-SR-ClientRadio.exe"
```

Expected: client window opens with the current light theme. Close it.

---

### Task 1: Bundle fonts

**Files:**
- Create: `IL2-SR-Client/Fonts/AllertaStencil-Regular.ttf`
- Create: `IL2-SR-Client/Fonts/AllertaStencil-LICENSE.txt`
- Create: `IL2-SR-Client/Fonts/ShareTechMono-Regular.ttf`
- Create: `IL2-SR-Client/Fonts/ShareTechMono-LICENSE.txt`
- Modify: `IL2-SR-Client/IL2-SR-Client.csproj` (around line 386, next to the existing JustAnotherHand entries)

- [ ] **Step 1.1: Download fonts (both are SIL OFL licensed, from the google/fonts repo)**

```powershell
Invoke-WebRequest "https://github.com/google/fonts/raw/main/ofl/allertastencil/AllertaStencil-Regular.ttf" -OutFile "IL2-SR-Client\Fonts\AllertaStencil-Regular.ttf"
Invoke-WebRequest "https://github.com/google/fonts/raw/main/ofl/allertastencil/OFL.txt" -OutFile "IL2-SR-Client\Fonts\AllertaStencil-LICENSE.txt"
Invoke-WebRequest "https://github.com/google/fonts/raw/main/ofl/sharetechmono/ShareTechMono-Regular.ttf" -OutFile "IL2-SR-Client\Fonts\ShareTechMono-Regular.ttf"
Invoke-WebRequest "https://github.com/google/fonts/raw/main/ofl/sharetechmono/OFL.txt" -OutFile "IL2-SR-Client\Fonts\ShareTechMono-LICENSE.txt"
```

- [ ] **Step 1.2: Verify downloads are real TTFs (>10 KB each, not error pages)**

```powershell
Get-ChildItem "IL2-SR-Client\Fonts\AllertaStencil-Regular.ttf","IL2-SR-Client\Fonts\ShareTechMono-Regular.ttf" | Select-Object Name, Length
```

Expected: both files > 10000 bytes. If a URL 404s, find the file at https://fonts.google.com (download family zip) — family names must be exactly "Allerta Stencil" and "Share Tech Mono".

- [ ] **Step 1.3: Register in csproj**

In `IL2-SR-Client/IL2-SR-Client.csproj`, find:

```xml
    <None Include="Fonts\JustAnotherHand-LICENSE.txt" />
    <Resource Include="Fonts\JustAnotherHand-Regular.ttf" />
```

Replace with:

```xml
    <None Include="Fonts\JustAnotherHand-LICENSE.txt" />
    <Resource Include="Fonts\JustAnotherHand-Regular.ttf" />
    <None Include="Fonts\AllertaStencil-LICENSE.txt" />
    <Resource Include="Fonts\AllertaStencil-Regular.ttf" />
    <None Include="Fonts\ShareTechMono-LICENSE.txt" />
    <Resource Include="Fonts\ShareTechMono-Regular.ttf" />
```

- [ ] **Step 1.4: Build** (same command as Step 0.2). Expected: success.

- [ ] **Step 1.5: Commit**

```powershell
git add IL2-SR-Client/Fonts IL2-SR-Client/IL2-SR-Client.csproj
git commit -m "Bundle Allerta Stencil and Share Tech Mono fonts (OFL)"
```

---

### Task 2: New value converters

**Files:**
- Create: `IL2-SR-Client/Utils/ValueConverters/BooleanToLedBrushConverter.cs`
- Create: `IL2-SR-Client/Utils/ValueConverters/StringToUpperConverter.cs`
- Modify: `IL2-SR-Client/IL2-SR-Client.csproj` (add two `<Compile>` entries next to the existing ValueConverters entries — search the csproj for `ValueConverters` to find the ItemGroup)

- [ ] **Step 2.1: Write `BooleanToLedBrushConverter.cs`**

```csharp
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Utils.ValueConverters
{
    public class BooleanToLedBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush OnBrush = CreateFrozen(0x8F, 0xB5, 0x73);
        private static readonly SolidColorBrush OffBrush = CreateFrozen(0x54, 0x56, 0x4F);

        private static SolidColorBrush CreateFrozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool isOn && isOn ? OnBrush : OffBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
```

- [ ] **Step 2.2: Write `StringToUpperConverter.cs`**

```csharp
using System;
using System.Globalization;
using System.Windows.Data;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Utils.ValueConverters
{
    public class StringToUpperConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString().ToUpper(CultureInfo.CurrentUICulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
```

- [ ] **Step 2.3: Register both files in the csproj** — find the `<Compile Include="Utils\ValueConverters\...` entries and add:

```xml
    <Compile Include="Utils\ValueConverters\BooleanToLedBrushConverter.cs" />
    <Compile Include="Utils\ValueConverters\StringToUpperConverter.cs" />
```

- [ ] **Step 2.4: Build.** Expected: success.

- [ ] **Step 2.5: Commit**

```powershell
git add IL2-SR-Client/Utils/ValueConverters IL2-SR-Client/IL2-SR-Client.csproj
git commit -m "Add LED brush and uppercase value converters"
```

---

### Task 3: Theme resource dictionaries

**Files:**
- Create: `IL2-SR-Client/Themes/MilitaryPalette.xaml`
- Create: `IL2-SR-Client/Themes/MilitaryControls.xaml`
- Modify: `IL2-SR-Client/IL2-SR-Client.csproj` (two `<Page>` entries next to `<Page Include="Themes\Styles.xaml">`)
- Modify: `IL2-SR-Client/App.xaml` (merge palette only)

- [ ] **Step 3.1: Write `Themes/MilitaryPalette.xaml`** (complete file):

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Military utility palette: see docs/superpowers/specs/2026-06-11-client-ui-redesign-design.md -->
    <Color x:Key="MilWindowBackgroundColor">#32342F</Color>
    <Color x:Key="MilChromeColor">#272924</Color>
    <Color x:Key="MilWellColor">#23251F</Color>
    <Color x:Key="MilBorderColor">#4A4D44</Color>
    <Color x:Key="MilTextPrimaryColor">#E4E6DD</Color>
    <Color x:Key="MilTextSecondaryColor">#878A7E</Color>
    <Color x:Key="MilAccentColor">#A8B06A</Color>
    <Color x:Key="MilActionColor">#5C6644</Color>
    <Color x:Key="MilActionBorderColor">#79855A</Color>
    <Color x:Key="MilActionHoverColor">#6B7651</Color>
    <Color x:Key="MilLedOnColor">#8FB573</Color>
    <Color x:Key="MilLedOffColor">#54564F</Color>
    <Color x:Key="MilLedErrorColor">#C25B4E</Color>
    <Color x:Key="MilRowAltColor">#2C2E29</Color>

    <SolidColorBrush x:Key="MilWindowBackgroundBrush" Color="{StaticResource MilWindowBackgroundColor}" />
    <SolidColorBrush x:Key="MilChromeBrush" Color="{StaticResource MilChromeColor}" />
    <SolidColorBrush x:Key="MilWellBrush" Color="{StaticResource MilWellColor}" />
    <SolidColorBrush x:Key="MilBorderBrush" Color="{StaticResource MilBorderColor}" />
    <SolidColorBrush x:Key="MilTextPrimaryBrush" Color="{StaticResource MilTextPrimaryColor}" />
    <SolidColorBrush x:Key="MilTextSecondaryBrush" Color="{StaticResource MilTextSecondaryColor}" />
    <SolidColorBrush x:Key="MilAccentBrush" Color="{StaticResource MilAccentColor}" />
    <SolidColorBrush x:Key="MilActionBrush" Color="{StaticResource MilActionColor}" />
    <SolidColorBrush x:Key="MilActionBorderBrush" Color="{StaticResource MilActionBorderColor}" />
    <SolidColorBrush x:Key="MilActionHoverBrush" Color="{StaticResource MilActionHoverColor}" />
    <SolidColorBrush x:Key="MilLedOnBrush" Color="{StaticResource MilLedOnColor}" />
    <SolidColorBrush x:Key="MilLedOffBrush" Color="{StaticResource MilLedOffColor}" />
    <SolidColorBrush x:Key="MilLedErrorBrush" Color="{StaticResource MilLedErrorColor}" />
    <SolidColorBrush x:Key="MilRowAltBrush" Color="{StaticResource MilRowAltColor}" />

    <FontFamily x:Key="MilFontStencil">pack://application:,,,/IL2-SR-ClientRadio;component/Fonts/#Allerta Stencil</FontFamily>
    <FontFamily x:Key="MilFontMono">pack://application:,,,/IL2-SR-ClientRadio;component/Fonts/#Share Tech Mono</FontFamily>

</ResourceDictionary>
```

- [ ] **Step 3.2: Write `Themes/MilitaryControls.xaml`** (complete file — implicit styles for every control type used in MainWindow, including the controls inside `InputBindingControl` and `FavouriteServersView` which render inside the main window):

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="pack://application:,,,/IL2-SR-ClientRadio;component/Themes/MilitaryPalette.xaml" />
    </ResourceDictionary.MergedDictionaries>

    <!-- ============ Labels & text ============ -->
    <Style TargetType="{x:Type Label}">
        <Setter Property="Foreground" Value="{StaticResource MilTextPrimaryBrush}" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="Padding" Value="4,2" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
    </Style>

    <!-- ============ Buttons ============ -->
    <Style x:Key="MilButtonBase" TargetType="{x:Type ButtonBase}">
        <Setter Property="Foreground" Value="{StaticResource MilTextPrimaryBrush}" />
        <Setter Property="Background" Value="{StaticResource MilChromeBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource MilBorderBrush}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Padding" Value="10,4" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="HorizontalContentAlignment" Value="Center" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
        <Setter Property="SnapsToDevicePixels" Value="True" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type ButtonBase}">
                    <Border x:Name="Bd"
                            Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="2"
                            Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                          VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
                                          RecognizesAccessKey="True" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="{StaticResource MilBorderBrush}" />
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="{StaticResource MilWellBrush}" />
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter Property="Foreground" Value="{StaticResource MilTextSecondaryBrush}" />
                            <Setter TargetName="Bd" Property="Background" Value="{StaticResource MilWellBrush}" />
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{StaticResource MilChromeBrush}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType="{x:Type Button}" BasedOn="{StaticResource MilButtonBase}" />

    <!-- Primary action style. MainWindow buttons reference SquareButtonStyle (a MahApps key)
         via DynamicResource; defining the same key here makes those buttons pick up the
         military action look with zero per-button XAML edits. -->
    <Style x:Key="SquareButtonStyle" TargetType="{x:Type Button}" BasedOn="{StaticResource MilButtonBase}">
        <Setter Property="Background" Value="{StaticResource MilActionBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource MilActionBorderBrush}" />
        <Setter Property="FontFamily" Value="{StaticResource MilFontMono}" />
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="{StaticResource MilActionHoverBrush}" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <!-- ============ ToggleButton (ON/OFF settings switches) ============ -->
    <Style TargetType="{x:Type ToggleButton}" BasedOn="{StaticResource MilButtonBase}">
        <Setter Property="MinWidth" Value="56" />
        <Setter Property="FontFamily" Value="{StaticResource MilFontMono}" />
        <Style.Triggers>
            <Trigger Property="IsChecked" Value="True">
                <Setter Property="Background" Value="{StaticResource MilActionBrush}" />
                <Setter Property="BorderBrush" Value="{StaticResource MilActionBorderBrush}" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <!-- ============ TextBox ============ -->
    <Style TargetType="{x:Type TextBox}">
        <Setter Property="Foreground" Value="{StaticResource MilTextPrimaryBrush}" />
        <Setter Property="Background" Value="{StaticResource MilWellBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource MilBorderBrush}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Padding" Value="4,2" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="CaretBrush" Value="{StaticResource MilTextPrimaryBrush}" />
        <Setter Property="SelectionBrush" Value="{StaticResource MilAccentBrush}" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
        <Style.Triggers>
            <Trigger Property="IsKeyboardFocused" Value="True">
                <Setter Property="BorderBrush" Value="{StaticResource MilAccentBrush}" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
                <Setter Property="Foreground" Value="{StaticResource MilTextSecondaryBrush}" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <!-- ============ ComboBox ============ -->
    <Style x:Key="MilComboToggle" TargetType="{x:Type ToggleButton}">
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type ToggleButton}">
                    <Border x:Name="Bd"
                            Background="{StaticResource MilWellBrush}"
                            BorderBrush="{StaticResource MilBorderBrush}"
                            BorderThickness="1"
                            CornerRadius="2">
                        <Path HorizontalAlignment="Right"
                              VerticalAlignment="Center"
                              Margin="0,0,8,0"
                              Data="M 0 0 L 4 4 L 8 0 Z"
                              Fill="{StaticResource MilAccentBrush}" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{StaticResource MilAccentBrush}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType="{x:Type ComboBox}">
        <Setter Property="Foreground" Value="{StaticResource MilTextPrimaryBrush}" />
        <Setter Property="Background" Value="{StaticResource MilWellBrush}" />
        <Setter Property="MinHeight" Value="24" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="SnapsToDevicePixels" Value="True" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type ComboBox}">
                    <Grid>
                        <ToggleButton x:Name="ToggleButton"
                                      Style="{StaticResource MilComboToggle}"
                                      Focusable="False"
                                      ClickMode="Press"
                                      IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}" />
                        <ContentPresenter x:Name="ContentSite"
                                          Margin="6,2,22,2"
                                          HorizontalAlignment="Left"
                                          VerticalAlignment="Center"
                                          Content="{TemplateBinding SelectionBoxItem}"
                                          ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                                          ContentTemplateSelector="{TemplateBinding ItemTemplateSelector}"
                                          IsHitTestVisible="False" />
                        <Popup x:Name="Popup"
                               Placement="Bottom"
                               IsOpen="{TemplateBinding IsDropDownOpen}"
                               AllowsTransparency="True"
                               Focusable="False"
                               PopupAnimation="Slide">
                            <Grid MinWidth="{TemplateBinding ActualWidth}"
                                  MaxHeight="{TemplateBinding MaxDropDownHeight}">
                                <Border Background="{StaticResource MilWellBrush}"
                                        BorderBrush="{StaticResource MilBorderBrush}"
                                        BorderThickness="1">
                                    <ScrollViewer SnapsToDevicePixels="True">
                                        <StackPanel IsItemsHost="True"
                                                    KeyboardNavigation.DirectionalNavigation="Contained" />
                                    </ScrollViewer>
                                </Border>
                            </Grid>
                        </Popup>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter Property="Foreground" Value="{StaticResource MilTextSecondaryBrush}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType="{x:Type ComboBoxItem}">
        <Setter Property="Foreground" Value="{StaticResource MilTextPrimaryBrush}" />
        <Setter Property="Padding" Value="6,3" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type ComboBoxItem}">
                    <Border x:Name="Bd" Background="Transparent" Padding="{TemplateBinding Padding}">
                        <ContentPresenter />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsHighlighted" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="{StaticResource MilActionBrush}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ============ CheckBox ============ -->
    <Style TargetType="{x:Type CheckBox}">
        <Setter Property="Foreground" Value="{StaticResource MilTextPrimaryBrush}" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type CheckBox}">
                    <StackPanel Orientation="Horizontal" Background="Transparent">
                        <Border x:Name="Box"
                                Width="14" Height="14"
                                VerticalAlignment="Center"
                                Background="{StaticResource MilWellBrush}"
                                BorderBrush="{StaticResource MilBorderBrush}"
                                BorderThickness="1">
                            <Path x:Name="Check"
                                  Data="M 2 7 L 6 11 L 12 3"
                                  Stroke="{StaticResource MilAccentBrush}"
                                  StrokeThickness="2"
                                  Visibility="Collapsed" />
                        </Border>
                        <ContentPresenter Margin="6,0,0,0" VerticalAlignment="Center" RecognizesAccessKey="True" />
                    </StackPanel>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="Check" Property="Visibility" Value="Visible" />
                        </Trigger>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Box" Property="BorderBrush" Value="{StaticResource MilAccentBrush}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ============ RadioButton ============ -->
    <Style TargetType="{x:Type RadioButton}">
        <Setter Property="Foreground" Value="{StaticResource MilTextPrimaryBrush}" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type RadioButton}">
                    <StackPanel Orientation="Horizontal" Background="Transparent">
                        <Grid VerticalAlignment="Center">
                            <Ellipse x:Name="Outer"
                                     Width="14" Height="14"
                                     Fill="{StaticResource MilWellBrush}"
                                     Stroke="{StaticResource MilBorderBrush}"
                                     StrokeThickness="1" />
                            <Ellipse x:Name="Dot"
                                     Width="6" Height="6"
                                     Fill="{StaticResource MilAccentBrush}"
                                     Visibility="Collapsed" />
                        </Grid>
                        <ContentPresenter Margin="6,0,0,0" VerticalAlignment="Center" RecognizesAccessKey="True" />
                    </StackPanel>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="Dot" Property="Visibility" Value="Visible" />
                        </Trigger>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Outer" Property="Stroke" Value="{StaticResource MilAccentBrush}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ============ Slider (horizontal only; the app has no vertical sliders in MainWindow) ============ -->
    <Style x:Key="MilSliderRepeatButton" TargetType="{x:Type RepeatButton}">
        <Setter Property="IsTabStop" Value="False" />
        <Setter Property="Focusable" Value="False" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type RepeatButton}">
                    <Rectangle Fill="Transparent" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key="MilSliderThumb" TargetType="{x:Type Thumb}">
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type Thumb}">
                    <Border Width="10" Height="18"
                            Background="{StaticResource MilAccentBrush}"
                            BorderBrush="{StaticResource MilChromeBrush}"
                            BorderThickness="1"
                            CornerRadius="1" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType="{x:Type Slider}">
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type Slider}">
                    <Grid MinHeight="18" Background="Transparent">
                        <Border Height="4"
                                VerticalAlignment="Center"
                                Background="{StaticResource MilWellBrush}"
                                BorderBrush="{StaticResource MilBorderBrush}"
                                BorderThickness="1" />
                        <Track x:Name="PART_Track">
                            <Track.DecreaseRepeatButton>
                                <RepeatButton Style="{StaticResource MilSliderRepeatButton}"
                                              Command="Slider.DecreaseLarge" />
                            </Track.DecreaseRepeatButton>
                            <Track.IncreaseRepeatButton>
                                <RepeatButton Style="{StaticResource MilSliderRepeatButton}"
                                              Command="Slider.IncreaseLarge" />
                            </Track.IncreaseRepeatButton>
                            <Track.Thumb>
                                <Thumb Style="{StaticResource MilSliderThumb}" />
                            </Track.Thumb>
                        </Track>
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ============ ProgressBar (VU meters) ============ -->
    <Style TargetType="{x:Type ProgressBar}">
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type ProgressBar}">
                    <Border Background="{StaticResource MilWellBrush}"
                            BorderBrush="{StaticResource MilBorderBrush}"
                            BorderThickness="1"
                            CornerRadius="1">
                        <Grid>
                            <Rectangle x:Name="PART_Track" />
                            <Decorator x:Name="PART_Indicator" HorizontalAlignment="Left">
                                <Rectangle Fill="{StaticResource MilLedOnBrush}" Margin="1" />
                            </Decorator>
                        </Grid>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ============ GroupBox: olive section header + rule, no box ============ -->
    <Style TargetType="{x:Type GroupBox}">
        <Setter Property="Foreground" Value="{StaticResource MilTextPrimaryBrush}" />
        <Setter Property="Margin" Value="0,4,0,4" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type GroupBox}">
                    <DockPanel>
                        <DockPanel DockPanel.Dock="Top" Margin="0,6,0,4" LastChildFill="True">
                            <TextBlock DockPanel.Dock="Left"
                                       Text="&#x25B8;"
                                       Foreground="{StaticResource MilAccentBrush}"
                                       FontSize="11"
                                       Margin="0,0,5,0"
                                       VerticalAlignment="Center" />
                            <ContentPresenter DockPanel.Dock="Left"
                                              ContentSource="Header"
                                              VerticalAlignment="Center"
                                              TextElement.Foreground="{StaticResource MilAccentBrush}"
                                              TextElement.FontFamily="{StaticResource MilFontMono}"
                                              TextElement.FontSize="11" />
                            <Border Height="1"
                                    Margin="8,1,0,0"
                                    VerticalAlignment="Center"
                                    Background="{StaticResource MilBorderBrush}" />
                        </DockPanel>
                        <ContentPresenter Margin="2,0,2,4" />
                    </DockPanel>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ============ Tabs ============ -->
    <Style TargetType="{x:Type TabControl}">
        <Setter Property="Background" Value="{StaticResource MilWindowBackgroundBrush}" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Padding" Value="4" />
    </Style>

    <Style TargetType="{x:Type TabItem}">
        <Setter Property="Foreground" Value="{StaticResource MilTextSecondaryBrush}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type TabItem}">
                    <Border x:Name="Bd"
                            Background="{StaticResource MilChromeBrush}"
                            BorderBrush="{StaticResource MilChromeBrush}"
                            BorderThickness="0,2,0,0"
                            Padding="14,5">
                        <ContentPresenter ContentSource="Header"
                                          HorizontalAlignment="Center"
                                          VerticalAlignment="Center" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsSelected" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="{StaticResource MilWindowBackgroundBrush}" />
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{StaticResource MilAccentBrush}" />
                            <Setter Property="Foreground" Value="{StaticResource MilTextPrimaryBrush}" />
                        </Trigger>
                        <MultiTrigger>
                            <MultiTrigger.Conditions>
                                <Condition Property="IsMouseOver" Value="True" />
                                <Condition Property="IsSelected" Value="False" />
                            </MultiTrigger.Conditions>
                            <Setter Property="Foreground" Value="{StaticResource MilTextPrimaryBrush}" />
                        </MultiTrigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ============ ScrollBar ============ -->
    <Style x:Key="MilScrollThumb" TargetType="{x:Type Thumb}">
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type Thumb}">
                    <Border Background="{StaticResource MilBorderBrush}" CornerRadius="3" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType="{x:Type ScrollBar}">
        <Setter Property="Background" Value="{StaticResource MilChromeBrush}" />
        <Setter Property="Width" Value="9" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type ScrollBar}">
                    <Border Background="{TemplateBinding Background}">
                        <Track x:Name="PART_Track" IsDirectionReversed="True">
                            <Track.DecreaseRepeatButton>
                                <RepeatButton Style="{StaticResource MilSliderRepeatButton}"
                                              Command="ScrollBar.PageUpCommand" />
                            </Track.DecreaseRepeatButton>
                            <Track.IncreaseRepeatButton>
                                <RepeatButton Style="{StaticResource MilSliderRepeatButton}"
                                              Command="ScrollBar.PageDownCommand" />
                            </Track.IncreaseRepeatButton>
                            <Track.Thumb>
                                <Thumb Style="{StaticResource MilScrollThumb}" Margin="1" />
                            </Track.Thumb>
                        </Track>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="Orientation" Value="Horizontal">
                <Setter Property="Width" Value="Auto" />
                <Setter Property="Height" Value="9" />
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="{x:Type ScrollBar}">
                            <Border Background="{TemplateBinding Background}">
                                <Track x:Name="PART_Track">
                                    <Track.DecreaseRepeatButton>
                                        <RepeatButton Style="{StaticResource MilSliderRepeatButton}"
                                                      Command="ScrollBar.PageLeftCommand" />
                                    </Track.DecreaseRepeatButton>
                                    <Track.IncreaseRepeatButton>
                                        <RepeatButton Style="{StaticResource MilSliderRepeatButton}"
                                                      Command="ScrollBar.PageRightCommand" />
                                    </Track.IncreaseRepeatButton>
                                    <Track.Thumb>
                                        <Thumb Style="{StaticResource MilScrollThumb}" Margin="1" />
                                    </Track.Thumb>
                                </Track>
                            </Border>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Trigger>
        </Style.Triggers>
    </Style>

    <!-- ============ ToolTip ============ -->
    <Style TargetType="{x:Type ToolTip}">
        <Setter Property="Foreground" Value="{StaticResource MilTextPrimaryBrush}" />
        <Setter Property="Background" Value="{StaticResource MilChromeBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource MilBorderBrush}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Padding" Value="8,4" />
        <Setter Property="FontSize" Value="12" />
    </Style>

    <!-- ============ DataGrid (FavouriteServersView renders inside MainWindow) ============ -->
    <Style TargetType="{x:Type DataGrid}">
        <Setter Property="Background" Value="{StaticResource MilWindowBackgroundBrush}" />
        <Setter Property="Foreground" Value="{StaticResource MilTextPrimaryBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource MilBorderBrush}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="RowBackground" Value="{StaticResource MilWellBrush}" />
        <Setter Property="AlternatingRowBackground" Value="{StaticResource MilRowAltBrush}" />
        <Setter Property="HorizontalGridLinesBrush" Value="{StaticResource MilChromeBrush}" />
        <Setter Property="VerticalGridLinesBrush" Value="{StaticResource MilChromeBrush}" />
        <Setter Property="FontSize" Value="12" />
    </Style>

    <Style TargetType="{x:Type DataGridColumnHeader}">
        <Setter Property="Background" Value="{StaticResource MilChromeBrush}" />
        <Setter Property="Foreground" Value="{StaticResource MilAccentBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource MilBorderBrush}" />
        <Setter Property="BorderThickness" Value="0,0,1,1" />
        <Setter Property="Padding" Value="6,4" />
        <Setter Property="FontFamily" Value="{StaticResource MilFontMono}" />
        <Setter Property="FontSize" Value="11" />
    </Style>

    <Style TargetType="{x:Type DataGridCell}">
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Padding" Value="4,2" />
        <Style.Triggers>
            <Trigger Property="IsSelected" Value="True">
                <Setter Property="Background" Value="{StaticResource MilActionBrush}" />
                <Setter Property="Foreground" Value="{StaticResource MilTextPrimaryBrush}" />
            </Trigger>
        </Style.Triggers>
    </Style>

</ResourceDictionary>
```

- [ ] **Step 3.3: Register both dictionaries in the csproj.** Find `<Page Include="Themes\Styles.xaml">` (line ~357) and add sibling entries with the same metadata shape:

```xml
    <Page Include="Themes\MilitaryPalette.xaml">
      <SubType>Designer</SubType>
      <Generator>MSBuild:Compile</Generator>
    </Page>
    <Page Include="Themes\MilitaryControls.xaml">
      <SubType>Designer</SubType>
      <Generator>MSBuild:Compile</Generator>
    </Page>
```

(Match the exact child elements of the existing `Themes\Styles.xaml` entry — copy its `<SubType>`/`<Generator>` children.)

- [ ] **Step 3.4: Merge the palette app-wide.** In `IL2-SR-Client/App.xaml`, after the line `<ResourceDictionary Source="Themes\Styles.xaml" />`, add:

```xml
                <ResourceDictionary Source="Themes\MilitaryPalette.xaml" />
```

(Palette only — keys are all `Mil*`-prefixed, no collisions. `MilitaryControls.xaml` is deliberately NOT merged here; it goes into MainWindow.Resources in Task 4.)

- [ ] **Step 3.5: Build.** Expected: success. (XAML parse errors here mean a typo in the dictionaries — fix before continuing.)

- [ ] **Step 3.6: Commit**

```powershell
git add IL2-SR-Client/Themes IL2-SR-Client/App.xaml IL2-SR-Client/IL2-SR-Client.csproj
git commit -m "Add military theme palette and control style dictionaries"
```

---

### Task 4: Apply theme to MainWindow chrome and tab strip

**Files:**
- Modify: `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml` (root element attributes, resources, TabControl ItemTemplate)

- [ ] **Step 4.1: Update the MetroWindow root element.** Replace the attribute block of the root `<controls:MetroWindow ...>` element (keep ALL existing xmlns declarations and `x:Class` exactly as-is) so the size/appearance attributes read:

```xml
                      Title="IL2-SRS Client"
                      Width="700"
                      MinWidth="640"
                      Height="650"
                      MinHeight="560"
                      Background="{StaticResource MilWindowBackgroundBrush}"
                      Foreground="{StaticResource MilTextPrimaryBrush}"
                      FontSize="12"
                      WindowTitleBrush="{StaticResource MilChromeBrush}"
                      NonActiveWindowTitleBrush="{StaticResource MilChromeBrush}"
                      GlowBrush="{StaticResource MilBorderBrush}"
                      NonActiveGlowBrush="{StaticResource MilChromeBrush}"
                      TitleCaps="False"
                      d:DataContext="{d:DesignInstance local:MainWindow}"
                      ResizeMode="CanResizeWithGrip"
                      mc:Ignorable="d">
```

If the build later errors that `TitleCaps`, `GlowBrush`, or `NonActiveWindowTitleBrush` is not found on this MahApps version, remove only the offending attribute and note it in the commit message.

- [ ] **Step 4.2: Add window resources + title template** immediately after the root element's opening tag (before `<TabControl ...>`):

```xml
    <controls:MetroWindow.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="pack://application:,,,/IL2-SR-ClientRadio;component/Themes/MilitaryControls.xaml" />
            </ResourceDictionary.MergedDictionaries>
            <converters:MicAvailabilityTooltipConverter x:Key="MicAvailabilityTooltipConverter" />
            <converters:BooleanToLedBrushConverter x:Key="LedBrushConverter" />
            <converters:StringToUpperConverter x:Key="StringToUpperConverter" />
        </ResourceDictionary>
    </controls:MetroWindow.Resources>

    <controls:MetroWindow.TitleTemplate>
        <DataTemplate>
            <TextBlock Margin="8,0,0,0"
                       VerticalAlignment="Center"
                       FontFamily="{StaticResource MilFontStencil}"
                       FontSize="14"
                       Foreground="{StaticResource MilTextPrimaryBrush}"
                       Text="{Binding}" />
        </DataTemplate>
    </controls:MetroWindow.TitleTemplate>
```

Then DELETE the old converter declaration inside the TabControl:

```xml
        <TabControl.Resources>
            <converters:MicAvailabilityTooltipConverter x:Key="MicAvailabilityTooltipConverter"/>
        </TabControl.Resources>
```

(The converter now lives in window resources; all existing `{StaticResource MicAvailabilityTooltipConverter}` references still resolve.)

- [ ] **Step 4.3: Replace the TabControl ItemTemplate.** Find:

```xml
        <TabControl.ItemTemplate>
            <DataTemplate>
                <TextBlock FontSize="22"
                           Text="{Binding}"
                           TextWrapping="NoWrap" />
            </DataTemplate>
        </TabControl.ItemTemplate>
```

Replace with:

```xml
        <TabControl.ItemTemplate>
            <DataTemplate>
                <TextBlock FontFamily="{StaticResource MilFontMono}"
                           FontSize="13"
                           Text="{Binding Converter={StaticResource StringToUpperConverter}}"
                           TextWrapping="NoWrap" />
            </DataTemplate>
        </TabControl.ItemTemplate>
```

- [ ] **Step 4.4: Build + launch.** Expected: dark grey-green window, dark title bar with stencil title, mono uppercase tab headers, all controls dark-styled (layout still the old one). Walk all 5 existing tabs — every control readable, no white-on-white. Known acceptable issue at this stage: hardcoded `Fill="Black"` gear icon next to the address box is invisible (it gets deleted in Task 7).

- [ ] **Step 4.5: Commit**

```powershell
git add IL2-SR-Client/UI/ClientWindow/MainWindow.xaml
git commit -m "Apply military theme to main window chrome, tabs and controls"
```

---

### Task 5: Remove the legacy Light/Dark/System theme system

**Files:**
- Delete: `IL2-SR-Client/Utils/ClientThemeManager.cs`
- Modify: `IL2-SR-Client/IL2-SR-Client.csproj` (remove `<Compile Include="Utils\ClientThemeManager.cs" />`, line ~297)
- Modify: `IL2-SR-Client/App.xaml.cs`
- Modify: `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml`
- Modify: `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml.cs`

The `GlobalSettingsKeys.Theme` enum member and its `"System"` default in `GlobalSettingsStore.cs` are **kept** so old config files load cleanly. Theme-related .resx entries are kept too.

- [ ] **Step 5.1: Remove theme picker row from MainWindow.xaml.** Delete the `<Label ... Content="Theme" />` (Grid.Row 19) and the entire `<WrapPanel Grid.Row="19" ...>` containing `LightThemeRadioButton`, `DarkThemeRadioButton`, `SystemThemeRadioButton`. Delete one `<RowDefinition />` from that grid's `Grid.RowDefinitions` (it has 36; theme was the last used row).

- [ ] **Step 5.2: Clean MainWindow.xaml.cs.** Remove, by searching for each symbol:
  - The call `ClientThemeManager.ApplyThemeToWindow(this, ...)` in the constructor (line ~131).
  - The line `TabControl.SelectionChanged += MainTabControl_SelectionChanged;` (line ~133) and the whole `MainTabControl_SelectionChanged` method — its only body is the theme call.
  - In `MainWindow_Loaded`: the `ApplyCurrentTheme();` call (keep `AutoStartRadioOverlay();`).
  - The whole `ApplyCurrentTheme()` method.
  - The whole `InitThemePicker()` method AND its call site (search `InitThemePicker(` — it is called from `InitSettingsScreen`).
  - The whole `ThemeRadioButton_Checked` method (line ~1795).
  - The field `_initialisingThemePicker` (search for it; remove declaration).

- [ ] **Step 5.3: Clean App.xaml.cs.** Remove:
  - The theme block in `OnStartup` (lines ~67–75): from `var theme = GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.Theme).RawValue;` through `SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;` inclusive.
  - The whole `SystemEvents_UserPreferenceChanged` method (line ~383).
  - The `using MahApps.Metro.Controls;` at the top only if the compiler then flags it unused; same for `Microsoft.Win32` usings — let the build tell you.

- [ ] **Step 5.4: Delete the file and its csproj entry**

```powershell
git rm IL2-SR-Client/Utils/ClientThemeManager.cs
```

Remove `<Compile Include="Utils\ClientThemeManager.cs" />` from the csproj.

- [ ] **Step 5.5: Build + launch.** Expected: builds with 0 errors (any "unused using" warnings fine; any leftover symbol reference = compile error pointing at what Step 5.2/5.3 missed). App launches with military theme; Settings tab no longer shows the Theme row; switching tabs doesn't repaint controls.

- [ ] **Step 5.6: Commit**

```powershell
git add -A
git commit -m "Remove legacy Light/Dark/System theme system"
```

---

### Task 6: Persistent status bar

**Files:**
- Modify: `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml`
- Modify: `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml.cs` (one added line)

- [ ] **Step 6.1: Wrap the TabControl in a DockPanel with a bottom status bar.** Change the window content from `<TabControl x:Name="TabControl">...</TabControl>` to:

```xml
    <DockPanel>
        <Border DockPanel.Dock="Bottom"
                Background="{StaticResource MilWellBrush}"
                BorderBrush="{StaticResource MilBorderBrush}"
                BorderThickness="0,1,0,0">
            <DockPanel Margin="10,3" LastChildFill="False">
                <StackPanel DockPanel.Dock="Left" Orientation="Horizontal">
                    <Ellipse Width="9" Height="9" VerticalAlignment="Center"
                             Fill="{Binding AudioInput.MicrophoneAvailable, Converter={StaticResource LedBrushConverter}}" />
                    <Label Content="Mic" Margin="1,0,10,0" Padding="2,0"
                           FontFamily="{StaticResource MilFontMono}" FontSize="11"
                           Foreground="{StaticResource MilTextSecondaryBrush}" />
                    <Ellipse Width="9" Height="9" VerticalAlignment="Center"
                             Fill="{Binding ClientState.IsConnected, Converter={StaticResource LedBrushConverter}}" />
                    <Label Content="Server" Margin="1,0,10,0" Padding="2,0"
                           FontFamily="{StaticResource MilFontMono}" FontSize="11"
                           Foreground="{StaticResource MilTextSecondaryBrush}" />
                    <Ellipse Width="9" Height="9" VerticalAlignment="Center"
                             Fill="{Binding ClientState.IsVoipConnected, Converter={StaticResource LedBrushConverter}}" />
                    <Label Content="VOIP" Margin="1,0,10,0" Padding="2,0"
                           FontFamily="{StaticResource MilFontMono}" FontSize="11"
                           Foreground="{StaticResource MilTextSecondaryBrush}" />
                    <Ellipse Width="9" Height="9" VerticalAlignment="Center"
                             Fill="{Binding ClientState.IsGameConnected, Converter={StaticResource LedBrushConverter}}" />
                    <Label Content="Il-2" Margin="1,0,0,0" Padding="2,0"
                           FontFamily="{StaticResource MilFontMono}" FontSize="11"
                           Foreground="{StaticResource MilTextSecondaryBrush}" />
                </StackPanel>
                <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
                    <Label Content="Connected Clients:" Padding="2,0"
                           FontFamily="{StaticResource MilFontMono}" FontSize="11"
                           Foreground="{StaticResource MilTextSecondaryBrush}" />
                    <TextBlock Text="{Binding Clients.Total}" VerticalAlignment="Center"
                               FontFamily="{StaticResource MilFontMono}" FontSize="11"
                               Foreground="{StaticResource MilTextPrimaryBrush}" />
                    <TextBlock Text="  |  " VerticalAlignment="Center" FontSize="11"
                               Foreground="{StaticResource MilBorderBrush}" />
                    <Label Name="CurrentProfile" Content="None" Padding="2,0"
                           FontFamily="{StaticResource MilFontMono}" FontSize="11"
                           Foreground="{StaticResource MilTextPrimaryBrush}" />
                    <TextBlock Text="  |  " VerticalAlignment="Center" FontSize="11"
                               Foreground="{StaticResource MilBorderBrush}" />
                    <TextBlock x:Name="StatusVersionLabel" VerticalAlignment="Center"
                               FontFamily="{StaticResource MilFontMono}" FontSize="11"
                               Foreground="{StaticResource MilTextSecondaryBrush}" />
                </StackPanel>
            </DockPanel>
        </Border>

        <TabControl x:Name="TabControl">
            <!-- existing TabControl content stays exactly where it was -->
        </TabControl>
    </DockPanel>
```

IMPORTANT before this compiles: the old General tab still contains a `<Label Name="CurrentProfile" ...>` — duplicate names are a XAML error. In the same step, in the General tab, DELETE:
  - The `StackPanel` containing `<Label Content="Current Profile:" />` and `<Label Name="CurrentProfile" Content="None" />` (the status bar owns the name now).
  - The `StackPanel` containing `<Label Content="Connected Clients:" />` and the `ClientCount` label with its MultiBinding.
  - The `StackPanel` with the `ConnectionStatusImageConverter` resource and the three `Image` elements `ServerConnectionStatus`, `VOIPConnectionStatus`, `GameConnectionStatus` plus their `Server`/`VOIP`/`Il-2` labels. (Nothing in code-behind references these three images — verified by grep.)

- [ ] **Step 6.2: Set the version text.** In `MainWindow.xaml.cs`, after the line `Title = Title + " - " + UpdaterChecker.RELEASE_TAG;` add:

```csharp
            StatusVersionLabel.Text = UpdaterChecker.RELEASE_TAG;
```

- [ ] **Step 6.3: Build + launch.** Expected: status bar visible on every tab. Mic LED green when a mic exists; Server/VOIP/Il-2 LEDs grey when disconnected; current profile shows (e.g. "default"); version tag shows. Old status images gone from General.

- [ ] **Step 6.4: Commit**

```powershell
git add IL2-SR-Client/UI/ClientWindow/MainWindow.xaml IL2-SR-Client/UI/ClientWindow/MainWindow.xaml.cs
git commit -m "Add persistent status bar with connection LEDs, clients, profile and version"
```

---

### Task 7: New Audio tab + General tab restructure + Favourites fold-in

This is the structural task. Every element listed "move" is cut/pasted **verbaten — same x:Name, bindings, Click handlers, Content strings** — only `Grid.Row` numbers and wrapping containers change.

**Files:**
- Modify: `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml`
- Modify: `IL2-SR-Client/UI/ClientWindow/MainWindow.xaml.cs` (delete one dead handler)

- [ ] **Step 7.1: Build the new Audio tab.** Insert after the Controls `</TabItem>` (and before the Favourites TabItem, which gets deleted in Step 7.3) this complete new TabItem. The inner elements marked `MOVED:` are the existing elements cut from their current homes — keep their exact current XAML; the skeleton below shows where each goes:

```xml
        <TabItem Header="Audio">
            <ScrollViewer HorizontalScrollBarVisibility="Disabled" VerticalScrollBarVisibility="Auto">
                <StackPanel Margin="10,0">
                    <GroupBox Header="Microphone">
                        <StackPanel HorizontalAlignment="Center">
                            <!-- MOVED: ComboBox x:Name="Mic" (from General/Setup) -->
                            <!-- MOVED: the StackPanel containing ProgressBar Name="Mic_VU" -->
                            <!-- MOVED: Button x:Name="Preview" -->
                        </StackPanel>
                    </GroupBox>
                    <GroupBox Header="Speakers &amp; Optional Mic Output">
                        <StackPanel HorizontalAlignment="Center">
                            <!-- MOVED: the StackPanel containing ComboBox x:Name="Speakers" and ComboBox x:Name="MicOutput" -->
                            <!-- MOVED: the StackPanel containing Slider x:Name="SpeakerBoost" and ProgressBar Name="Speaker_VU" -->
                            <!-- MOVED: the StackPanel containing Label Content="Speaker Boost:" and Label x:Name="SpeakerBoostLabel" -->
                        </StackPanel>
                    </GroupBox>
                    <GroupBox Header="Audio Options">
                        <Grid HorizontalAlignment="Center">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="390" />
                                <ColumnDefinition />
                            </Grid.ColumnDefinitions>
                            <Grid.RowDefinitions>
                                <RowDefinition />
                                <RowDefinition />
                                <RowDefinition />
                                <RowDefinition />
                            </Grid.RowDefinitions>
                            <!-- MOVED from Settings/Global Settings grid, renumbered Grid.Row 0-3: -->
                            <!-- Row 0: Label "Allow More Input Devices" + ToggleButton Name="ExpandInputDevices" (with its inline ON/OFF style) -->
                            <!-- Row 1: Label "Microphone Automatic Gain Control" + ToggleButton Name="MicAGC" -->
                            <!-- Row 2: Label "Microphone Noise Suppression" + ToggleButton Name="MicDenoise" -->
                            <!-- Row 3: Label "Play connection sounds" + ToggleButton Name="PlayConnectionSounds" -->
                        </Grid>
                    </GroupBox>
                </StackPanel>
            </ScrollViewer>
        </TabItem>
```

The two standalone labels `MicLabel` ("Microphone") and `SpeakerLabel` ("Speakers & Optional Mic Output") are NOT moved — they are deleted; the GroupBox headers replace them. (Verified by grep: no code-behind references either label.)

- [ ] **Step 7.2: Renumber the Settings global grid.** After cutting the four rows out of the Global Settings grid, renumber the remaining `Grid.Row` values of every Label/ToggleButton/Button/ComboBox in that grid so they're consecutive from 0 (order preserved), and delete four more `<RowDefinition />` elements. Resulting row order: Auto Connect Prompt, Auto Connect Mismatch Prompt, Reset Radio Overlay, Hide Overlay Taskbar Item, Auto Start Radio Overlay, Autostart Roster Overlay, Auto Refocus IL2, Minimise to tray, Start minimised, Check for beta updates, Require Admin, Show Transmitter Name, Language.

- [ ] **Step 7.3: Rebuild the General tab.** Replace the General TabItem's single `GroupBox Header="Setup"` wrapper with this structure (moved elements verbatim):

```xml
        <TabItem Header="General">
            <ScrollViewer HorizontalScrollBarVisibility="Disabled" VerticalScrollBarVisibility="Auto">
                <StackPanel Margin="10,0">
                    <GroupBox Header="Server">
                        <StackPanel HorizontalAlignment="Center">
                            <!-- MOVED: the StackPanel containing TextBox x:Name="ServerIp" and ComboBox x:Name="ServerAddressPicker".
                                 DELETE the gear Button (Click="LaunchAddressTab") from inside it - favourites
                                 are now on this same tab. -->
                            <!-- MOVED: the StackPanel containing Button x:Name="StartStop" and Button x:Name="ToggleServerSettings" -->
                        </StackPanel>
                    </GroupBox>
                    <GroupBox Header="Favourites">
                        <!-- MOVED from the Favourites TabItem: -->
                        <clientWindow:FavouriteServersView DataContext="{Binding Path=FavouriteServersViewModel}" MaxHeight="230" />
                    </GroupBox>
                    <GroupBox Header="Overlays">
                        <StackPanel HorizontalAlignment="Center">
                            <!-- MOVED: the StackPanel containing ShowOverlay + ShowClientList buttons -->
                            <!-- MOVED: the StackPanel containing ShowPilotRoster button -->
                            <!-- MOVED: the StackPanel containing the Patreon button (Click="Donate_OnClick") -->
                            <!-- MOVED: the entire StackPanel x:Name="RciStatusPanel" verbatim -->
                        </StackPanel>
                    </GroupBox>
                </StackPanel>
            </ScrollViewer>
        </TabItem>
```

Also delete the now-empty decorative StackPanels from the old Setup group (the ones containing only empty nested StackPanels and the `BooleanInverterConverter` resource — KEEP the converter declaration ONLY if a grep for `BooleanInverterConverter` inside MainWindow.xaml finds a consumer; if no consumer, delete it with its StackPanel).

- [ ] **Step 7.4: Delete the Favourites TabItem** (`<TabItem x:Name="FavouritesSeversTab" Header="Favourites">...</TabItem>`) — its content moved in Step 7.3.

- [ ] **Step 7.5: Remove the dead handler.** In `MainWindow.xaml.cs`, find and delete the `LaunchAddressTab` method (it selected `FavouritesSeversTab`, which no longer exists). Grep for `FavouritesSeversTab` afterwards — zero remaining references expected.

- [ ] **Step 7.6: Build + launch + functional pass.** Expected:
  - 5 tabs: GENERAL, CONTROLS, AUDIO, SETTINGS, HELP (in that order).
  - General: server box + connect, favourites grid functional (add/select favourite, picker updates), overlay/client-list/roster buttons work, Patreon opens browser.
  - Audio: mic select + VU meter moves when speaking, preview works, speaker boost slider updates dB label, 4 toggles flip ON/OFF and persist after restart.
  - Settings: remaining toggles aligned correctly (no overlapping rows — if rows overlap, Step 7.2 renumbering missed something).
  - No XAML duplicate-name or missing-handler build errors.

- [ ] **Step 7.7: Commit**

```powershell
git add IL2-SR-Client/UI/ClientWindow/MainWindow.xaml IL2-SR-Client/UI/ClientWindow/MainWindow.xaml.cs
git commit -m "Restructure main window: new Audio tab, favourites folded into General"
```

---

### Task 8: Localization keys for new strings

**Files:**
- Modify: `IL2-SR-Client/Localization/en.resx`, `de.resx`, `fr.resx`, `es.resx`, `it.resx`, `ru.resx`

New UI strings introduced: tab header `Audio`, group header `Audio Options`, group header `Overlays`, status label `Mic`. (`Server`, `Favourites`, `VOIP`, `Il-2`, `Connected Clients:`, `Microphone`, `Speakers & Optional Mic Output` already exist as keys.) `LocalizationManager.Get` falls back to the English text for missing keys, so this task is polish, not a crash fix.

- [ ] **Step 8.1: Check which keys already exist** (some may): search each resx for `name="Audio"`, `name="Audio Options"`, `name="Overlays"`, `name="Mic"`. Only add missing ones.

- [ ] **Step 8.2: Add entries.** Follow the existing `<data>` format in each file. Values:

| Key | en | de | fr | es | it | ru |
|---|---|---|---|---|---|---|
| Audio | Audio | Audio | Audio | Audio | Audio | Аудио |
| Audio Options | Audio Options | Audio-Optionen | Options audio | Opciones de audio | Opzioni audio | Параметры аудио |
| Overlays | Overlays | Overlays | Overlays | Superposiciones | Overlay | Оверлеи |
| Mic | Mic | Mikro | Micro | Micro | Micro | Микр |

Format per entry (example for de.resx):

```xml
  <data name="Audio Options" xml:space="preserve">
    <value>Audio-Optionen</value>
  </data>
```

- [ ] **Step 8.3: Build + launch with a non-English language** (Settings → Language → Deutsch). Expected: AUDIO tab header and group headers translated; everything else unchanged.

- [ ] **Step 8.4: Commit**

```powershell
git add IL2-SR-Client/Localization
git commit -m "Add localization keys for Audio tab, Overlays group and status bar"
```

---

### Task 9: Final verification sweep

**Files:** none (fixes only if issues found).

- [ ] **Step 9.1: Full solution build**

```powershell
& $msbuild "IL2-SimpleRadioStandalone.sln" /p:Configuration=Debug /v:m /nologo
```

Expected: all projects build.

- [ ] **Step 9.2: Run the common tests**

```powershell
& $msbuild "IL2-SR-CommonTests\IL2-SR-CommonTests.csproj" /p:Configuration=Debug /v:m /nologo
$vstest = & "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" -latest -find "**\TestPlatform\vstest.console.exe" | Select-Object -First 1
& $vstest "IL2-SR-CommonTests\bin\Debug\IL2-SR-CommonTests.dll"
```

Expected: all tests pass (these don't touch the client, so any failure is pre-existing — compare against a run on the base commit before blaming the redesign).

- [ ] **Step 9.3: Back-compat config check.** Locate the client settings file (search `GlobalSettingsStore.cs` for the cfg filename/path). Ensure it contains `Theme=Dark` (add the line if absent), launch the client. Expected: launches normally, value silently ignored.

- [ ] **Step 9.4: Popup smoke test.** With the client running: open Client List, Pilot Roster, Server Settings (connect to a server first if needed — a local server can be run from `IL2-SimpleRadio Server`), and the input prompt (Controls → click a binding). Expected: all open and are usable. They retain MahApps light styling (implicit military styles are scoped to MainWindow) — that is BY DESIGN per the plan header note.
- [ ] **Step 9.5: Overlay regression check.** Toggle the Radio Overlay from General. Expected: overlay looks IDENTICAL to before the redesign (it must not pick up any military styles).

- [ ] **Step 9.6: DPI check.** Set Windows display scaling to 150% (or use a secondary monitor with different scaling), launch, walk tabs. Expected: no clipped controls.

- [ ] **Step 9.7: Update the spec** — in `docs/superpowers/specs/2026-06-11-client-ui-redesign-design.md`, amend the App.xaml row of the architecture table to note that `MilitaryControls.xaml` merges into MainWindow.Resources (overlay protection), and the popup-inheritance risk row accordingly.

- [ ] **Step 9.8: Final commit**

```powershell
git add -A
git commit -m "Finalize military UI redesign: spec amendment and verification fixes"
```

---

## Self-review notes (completed during planning)

- **Spec coverage:** palette/typography/controls → Tasks 1–4; window structure + status bar → Tasks 4, 6; tab reorganization → Task 7; ClientThemeManager removal + back-compat → Task 5, Step 9.3; localization → Task 8; verification plan → every task + Task 9. Spec's App.xaml merge point deliberately refined (see header) — spec amended in Step 9.7.
- **Known unknowns the executor must verify in place:** MahApps 1.5 property names on MetroWindow (`TitleCaps`/`GlowBrush` — Step 4.1 has the fallback), exact `Grid.Row` indices in the Settings grid (Step 7.2 lists the expected final order), `BooleanInverterConverter` consumer check (Step 7.3), existing resx keys (Step 8.1).
- **Type consistency:** converter keys `LedBrushConverter`/`StringToUpperConverter` defined in Task 4 Step 4.2, consumed in Tasks 4 (tab template) and 6 (status bar). Brush keys all `Mil*` from Task 3, consumed in Tasks 4, 6, 7. Pack URIs all use assembly `IL2-SR-ClientRadio` (verify once in Task 0).
