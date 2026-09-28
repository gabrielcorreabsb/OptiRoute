# OptiRoute Design System

> Single source of truth for visual design, reusable components, and interaction patterns across the WPF/.NET 10 OptiRoute app.  
> **Scope:** `MainWindow.xaml`, `SettingsWindow.xaml`, future `AboutWindow`, tray menu, and installer UI.  
> **Theme:** dark only.  
> **Tech:** WPF XAML, static resources, no custom markup extensions, no new NuGet packages.

---

## 1. Design Tokens

All tokens live in `src/OptiRoute.App/Themes/Styles.xaml` as `SolidColorBrush`, `Thickness`, `CornerRadius`, or numeric resources. Reference everywhere via `{StaticResource TokenName}`.

### 1.1 Color palette

| Token | Hex | Tailwind base | Usage |
|-------|-----|---------------|-------|
| `Tokens.Surface.Window` | `#18181B` | zinc-900 | Window background |
| `Tokens.Surface.Page` | `#18181B` | zinc-900 | Page / dialog background |
| `Tokens.Surface.Card` | `#27272A` | zinc-800 | Card, panel, footer surfaces |
| `Tokens.Surface.CardHover` | `#3F3F46` | zinc-700 | Card hover / active input bg |
| `Tokens.Surface.CardActive` | `#52525B` | zinc-600 | Pressed card, pressed secondary button |
| `Tokens.Surface.Input` | `#18181B` | zinc-900 | TextBox, PasswordBox, ComboBox background |
| `Tokens.Surface.Elevated` | `#27272A` | zinc-800 | Flyout, menu, tooltip background |
| `Tokens.Divider.Default` | `#3F3F46` | zinc-700 | Borders, separators, grid lines |
| `Tokens.Divider.Subtle` | `#27272A` | zinc-800 | Invisible dividers / spacer rules |
| `Tokens.Accent.100` | `#DBEAFE` | blue-100 | Inverse text on accent bg |
| `Tokens.Accent.300` | `#93C5FD` | blue-300 | Badge text, secondary accent |
| `Tokens.Accent.500` | `#3B82F6` | blue-500 | Primary buttons, links, progress, selected tabs |
| `Tokens.Accent.600` | `#2563EB` | blue-600 | Primary hover |
| `Tokens.Accent.700` | `#1D4ED8` | blue-700 | Primary pressed |
| `Tokens.Accent.900` | `#1E3A8A` | blue-900 | Accent badges / info panels |
| `Tokens.Semantic.Success` | `#10B981` | emerald-500 | Online dot, success text |
| `Tokens.Semantic.SuccessBg` | `#064E3B` | emerald-900 | Success box background |
| `Tokens.Semantic.SuccessBorder` | `#34D399` | emerald-400 | Success box border |
| `Tokens.Semantic.Warning` | `#F59E0B` | amber-500 | Warning icon / text |
| `Tokens.Semantic.WarningBg` | `#451A03` | amber-900 | Warning box background |
| `Tokens.Semantic.WarningBorder` | `#FBBF24` | amber-400 | Warning box border |
| `Tokens.Semantic.Error` | `#EF4444` | red-500 | Error icon / text / validation border |
| `Tokens.Semantic.ErrorBg` | `#450A0A` | red-950 | Error box background |
| `Tokens.Semantic.ErrorBorder` | `#F87171` | red-400 | Error box border |
| `Tokens.Semantic.Info` | `#60A5FA` | blue-400 | Info icon / text |
| `Tokens.Semantic.InfoBg` | `#1E3A8A` | blue-900 | Info box background |
| `Tokens.Semantic.InfoBorder` | `#60A5FA` | blue-400 | Info box border |
| `Tokens.Text.Primary` | `#F4F4F5` | zinc-100 | Headings, primary body |
| `Tokens.Text.Secondary` | `#E4E4E7` | zinc-200 | Emphasized body, labels |
| `Tokens.Text.Dim` | `#A1A1AA` | zinc-400 | Captions, placeholders, disabled text on surface |
| `Tokens.Text.Disabled` | `#71717A` | zinc-500 | Disabled control foreground |
| `Tokens.Text.Inverse` | `#FFFFFF` | white | Text on accent / colored backgrounds |

### 1.2 Spacing scale

| Token | Value | Usage |
|-------|-------|-------|
| `Tokens.Space.1` | `4` | Tight internal gaps, icon/text spacing |
| `Tokens.Space.2` | `8` | Default inner control padding (vertical) |
| `Tokens.Space.3` | `12` | Card internal padding, small section gaps |
| `Tokens.Space.4` | `16` | Page margins, card padding |
| `Tokens.Space.5` | `24` | Section vertical gaps |
| `Tokens.Space.6` | `32` | Hero / empty state padding |
| `Tokens.Space.8` | `48` | Wizard hero margins |
| `Tokens.Space.10` | `64` | Full-screen empty state gaps |

All spacing values are device-independent pixels. Use as `Margin="{StaticResource Tokens.Space.4}"` via `Thickness` resource, or as numeric tokens for custom multipliers.

### 1.3 Radius

| Token | Value | Usage |
|-------|-------|-------|
| `Tokens.Radius.Small` | `4` | Small badges, chips, compact buttons |
| `Tokens.Radius.Medium` | `6` | Buttons, inputs, small cards |
| `Tokens.Radius.Large` | `8` | Cards, panels, footer bars |
| `Tokens.Radius.XLarge` | `12` | Hero panels, modals, wizard welcome |

### 1.4 Typography

Use system fonts. WPF default is acceptable because the visual identity comes from color, spacing, and layout, but **do not** explicitly set Arial/Inter. Leave `FontFamily` unset on most controls so the system font (Segoe UI on Windows) applies.

| Token | Size | Weight | Line height | Usage |
|-------|------|--------|-------------|-------|
| `Tokens.Text.Display` | `32` | Bold (700) | `40` | Wizard title, large empty state |
| `Tokens.Text.H1` | `24` | Bold (700) | `32` | Main window brand / page title |
| `Tokens.Text.H2` | `18` | SemiBold (600) | `28` | Section headers, card titles |
| `Tokens.Text.Body` | `14` | Regular (400) | `20` | Labels, body text, inputs |
| `Tokens.Text.BodyStrong` | `14` | SemiBold (600) | `20` | Emphasized body, tab labels |
| `Tokens.Text.Caption` | `12` | Regular (400) | `16` | Badges, hints, status text |
| `Tokens.Text.Small` | `11` | SemiBold (600) | `16` | Footer version, micro labels |

Line height is expressed as the recommended `LineStackingStrategy="MaxHeight"` / `LineHeight` value in XAML.

### 1.5 Elevation

| Token | Shadow | Usage |
|-------|--------|-------|
| `Tokens.Elevation.0` | none | Flat surfaces, base window |
| `Tokens.Elevation.1` | `0 1 2 0 rgba(0,0,0,0.25)` | Cards, footer |
| `Tokens.Elevation.2` | `0 4 12 0 rgba(0,0,0,0.35)` | Dropdowns, menus |
| `Tokens.Elevation.3` | `0 8 24 0 rgba(0,0,0,0.45)` | Modals, dialogs |

Because WPF does not have a universal `box-shadow` property, elevation is implemented with a `Border.Effect` / `DropShadowEffect` or, preferably in the card pattern, by stacking a `Border` with `DropShadowEffect`.

### 1.6 Motion

| Token | Duration | Easing | Usage |
|-------|----------|--------|-------|
| `Tokens.Duration.Instant` | `0` | — | Instant toggles, no animation |
| `Tokens.Duration.Fast` | `150ms` | `0.4,0,0.2,1` (ease-out) | Button hover, focus ring |
| `Tokens.Duration.Medium` | `250ms` | `0.4,0,0.2,1` | Tab underline, panel transitions |
| `Tokens.Duration.Slow` | `400ms` | `0.4,0,0.2,1` | Page / wizard step fade |

Implemented via `Duration` on `ColorAnimation` / `DoubleAnimation` inside style triggers.

### 1.7 Token definitions in `Styles.xaml`

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=mscorlib">

  <!-- ════════════════════════════════════════════════════════════════════
       SURFACE
       ════════════════════════════════════════════════════════════════════ -->
  <SolidColorBrush x:Key="Tokens.Surface.Window"         Color="#18181B"/>
  <SolidColorBrush x:Key="Tokens.Surface.Page"           Color="#18181B"/>
  <SolidColorBrush x:Key="Tokens.Surface.Card"           Color="#27272A"/>
  <SolidColorBrush x:Key="Tokens.Surface.CardHover"      Color="#3F3F46"/>
  <SolidColorBrush x:Key="Tokens.Surface.CardActive"     Color="#52525B"/>
  <SolidColorBrush x:Key="Tokens.Surface.Input"          Color="#18181B"/>
  <SolidColorBrush x:Key="Tokens.Surface.Elevated"       Color="#27272A"/>

  <SolidColorBrush x:Key="Tokens.Divider.Default"        Color="#3F3F46"/>
  <SolidColorBrush x:Key="Tokens.Divider.Subtle"         Color="#27272A"/>

  <!-- ════════════════════════════════════════════════════════════════════
       ACCENT — Blue ramp
       ════════════════════════════════════════════════════════════════════ -->
  <SolidColorBrush x:Key="Tokens.Accent.100"             Color="#DBEAFE"/>
  <SolidColorBrush x:Key="Tokens.Accent.300"             Color="#93C5FD"/>
  <SolidColorBrush x:Key="Tokens.Accent.500"             Color="#3B82F6"/>
  <SolidColorBrush x:Key="Tokens.Accent.600"             Color="#2563EB"/>
  <SolidColorBrush x:Key="Tokens.Accent.700"             Color="#1D4ED8"/>
  <SolidColorBrush x:Key="Tokens.Accent.900"             Color="#1E3A8A"/>

  <!-- ════════════════════════════════════════════════════════════════════
       SEMANTIC
       ════════════════════════════════════════════════════════════════════ -->
  <SolidColorBrush x:Key="Tokens.Semantic.Success"       Color="#10B981"/>
  <SolidColorBrush x:Key="Tokens.Semantic.SuccessBg"     Color="#064E3B"/>
  <SolidColorBrush x:Key="Tokens.Semantic.SuccessBorder" Color="#34D399"/>

  <SolidColorBrush x:Key="Tokens.Semantic.Warning"       Color="#F59E0B"/>
  <SolidColorBrush x:Key="Tokens.Semantic.WarningBg"     Color="#451A03"/>
  <SolidColorBrush x:Key="Tokens.Semantic.WarningBorder" Color="#FBBF24"/>

  <SolidColorBrush x:Key="Tokens.Semantic.Error"         Color="#EF4444"/>
  <SolidColorBrush x:Key="Tokens.Semantic.ErrorBg"       Color="#450A0A"/>
  <SolidColorBrush x:Key="Tokens.Semantic.ErrorBorder"   Color="#F87171"/>

  <SolidColorBrush x:Key="Tokens.Semantic.Info"          Color="#60A5FA"/>
  <SolidColorBrush x:Key="Tokens.Semantic.InfoBg"        Color="#1E3A8A"/>
  <SolidColorBrush x:Key="Tokens.Semantic.InfoBorder"    Color="#60A5FA"/>

  <!-- ════════════════════════════════════════════════════════════════════
       TEXT
       ════════════════════════════════════════════════════════════════════ -->
  <SolidColorBrush x:Key="Tokens.Text.Primary"           Color="#F4F4F5"/>
  <SolidColorBrush x:Key="Tokens.Text.Secondary"         Color="#E4E4E7"/>
  <SolidColorBrush x:Key="Tokens.Text.Dim"               Color="#A1A1AA"/>
  <SolidColorBrush x:Key="Tokens.Text.Disabled"          Color="#71717A"/>
  <SolidColorBrush x:Key="Tokens.Text.Inverse"           Color="#FFFFFF"/>

  <!-- ════════════════════════════════════════════════════════════════════
       SPACING (numeric + thickness)
       ════════════════════════════════════════════════════════════════════ -->
  <sys:Double x:Key="Tokens.Space.1">4</sys:Double>
  <sys:Double x:Key="Tokens.Space.2">8</sys:Double>
  <sys:Double x:Key="Tokens.Space.3">12</sys:Double>
  <sys:Double x:Key="Tokens.Space.4">16</sys:Double>
  <sys:Double x:Key="Tokens.Space.5">24</sys:Double>
  <sys:Double x:Key="Tokens.Space.6">32</sys:Double>
  <sys:Double x:Key="Tokens.Space.8">48</sys:Double>
  <sys:Double x:Key="Tokens.Space.10">64</sys:Double>

  <Thickness x:Key="Tokens.Thickness.1">4</Thickness>
  <Thickness x:Key="Tokens.Thickness.2">8</Thickness>
  <Thickness x:Key="Tokens.Thickness.3">12</Thickness>
  <Thickness x:Key="Tokens.Thickness.4">16</Thickness>
  <Thickness x:Key="Tokens.Thickness.5">24</Thickness>
  <Thickness x:Key="Tokens.Thickness.6">32</Thickness>

  <!-- ════════════════════════════════════════════════════════════════════
       RADIUS
       ════════════════════════════════════════════════════════════════════ -->
  <CornerRadius x:Key="Tokens.Radius.Small">4</CornerRadius>
  <CornerRadius x:Key="Tokens.Radius.Medium">6</CornerRadius>
  <CornerRadius x:Key="Tokens.Radius.Large">8</CornerRadius>
  <CornerRadius x:Key="Tokens.Radius.XLarge">12</CornerRadius>

  <!-- ════════════════════════════════════════════════════════════════════
       ELEVATION
       ════════════════════════════════════════════════════════════════════ -->
  <DropShadowEffect x:Key="Tokens.Elevation.0" BlurRadius="0" ShadowDepth="0" Opacity="0"/>
  <DropShadowEffect x:Key="Tokens.Elevation.1" BlurRadius="2" ShadowDepth="1" Opacity="0.25" Color="Black"/>
  <DropShadowEffect x:Key="Tokens.Elevation.2" BlurRadius="12" ShadowDepth="4" Opacity="0.35" Color="Black"/>
  <DropShadowEffect x:Key="Tokens.Elevation.3" BlurRadius="24" ShadowDepth="8" Opacity="0.45" Color="Black"/>

  <!-- ════════════════════════════════════════════════════════════════════
       DURATION
       ════════════════════════════════════════════════════════════════════ -->
  <Duration x:Key="Tokens.Duration.Instant">0:0:0</Duration>
  <Duration x:Key="Tokens.Duration.Fast">0:0:0.15</Duration>
  <Duration x:Key="Tokens.Duration.Medium">0:0:0.25</Duration>
  <Duration x:Key="Tokens.Duration.Slow">0:0:0.4</Duration>

</ResourceDictionary>
```

---

## 2. Component Library

All reusable styles are defined in `Styles.xaml` unless stated otherwise. Use `{StaticResource Styles.XXX}` to apply them.

### 2.1 Buttons

**Root cause of white buttons:** WPF Aero theme uses the default `ButtonChrome`, which ignores `Background`. Every style below overrides `Template` with a custom `Border` + `ContentPresenter` so the `Background` property is honored.

#### 2.1.1 `Styles.PrimaryButton`

```xml
<Style x:Key="Styles.PrimaryButton" TargetType="Button">
  <Setter Property="Background"      Value="{StaticResource Tokens.Accent.500}"/>
  <Setter Property="Foreground"      Value="{StaticResource Tokens.Text.Inverse}"/>
  <Setter Property="FontWeight"      Value="SemiBold"/>
  <Setter Property="FontSize"        Value="14"/>
  <Setter Property="Padding"         Value="14,8"/>
  <Setter Property="BorderThickness" Value="0"/>
  <Setter Property="Cursor"          Value="Hand"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="Button">
        <Border x:Name="Chrome"
                Background="{TemplateBinding Background}"
                BorderBrush="{TemplateBinding BorderBrush}"
                BorderThickness="{TemplateBinding BorderThickness}"
                CornerRadius="{StaticResource Tokens.Radius.Medium}"
                Padding="{TemplateBinding Padding}"
                SnapsToDevicePixels="True">
          <ContentPresenter HorizontalAlignment="Center"
                            VerticalAlignment="Center"
                            RecognizesAccessKey="True"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter TargetName="Chrome" Property="Background" Value="{StaticResource Tokens.Accent.600}"/>
          </Trigger>
          <Trigger Property="IsPressed" Value="True">
            <Setter TargetName="Chrome" Property="Background" Value="{StaticResource Tokens.Accent.700}"/>
          </Trigger>
          <Trigger Property="IsEnabled" Value="False">
            <Setter TargetName="Chrome" Property="Background" Value="{StaticResource Tokens.Surface.CardActive}"/>
            <Setter Property="Foreground" Value="{StaticResource Tokens.Text.Disabled}"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

#### 2.1.2 `Styles.SecondaryButton`

```xml
<Style x:Key="Styles.SecondaryButton" TargetType="Button" BasedOn="{StaticResource Styles.PrimaryButton}">
  <Setter Property="Background" Value="{StaticResource Tokens.Surface.CardHover}"/>
  <Setter Property="Foreground" Value="{StaticResource Tokens.Text.Primary}"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="Button">
        <Border x:Name="Chrome"
                Background="{TemplateBinding Background}"
                BorderBrush="{StaticResource Tokens.Divider.Default}"
                BorderThickness="1"
                CornerRadius="{StaticResource Tokens.Radius.Medium}"
                Padding="{TemplateBinding Padding}"
                SnapsToDevicePixels="True">
          <ContentPresenter HorizontalAlignment="Center"
                            VerticalAlignment="Center"
                            RecognizesAccessKey="True"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter TargetName="Chrome" Property="Background" Value="{StaticResource Tokens.Surface.CardActive}"/>
            <Setter TargetName="Chrome" Property="BorderBrush" Value="{StaticResource Tokens.Text.Dim}"/>
          </Trigger>
          <Trigger Property="IsPressed" Value="True">
            <Setter TargetName="Chrome" Property="Background" Value="{StaticResource Tokens.Divider.Default}"/>
          </Trigger>
          <Trigger Property="IsEnabled" Value="False">
            <Setter TargetName="Chrome" Property="Background" Value="{StaticResource Tokens.Surface.Card}"/>
            <Setter TargetName="Chrome" Property="BorderBrush" Value="{StaticResource Tokens.Divider.Subtle}"/>
            <Setter Property="Foreground" Value="{StaticResource Tokens.Text.Disabled}"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

#### 2.1.3 `Styles.TertiaryButton` (link style)

```xml
<Style x:Key="Styles.TertiaryButton" TargetType="Button">
  <Setter Property="Background"      Value="Transparent"/>
  <Setter Property="Foreground"      Value="{StaticResource Tokens.Accent.300}"/>
  <Setter Property="FontWeight"      Value="SemiBold"/>
  <Setter Property="FontSize"        Value="13"/>
  <Setter Property="Padding"         Value="4,2"/>
  <Setter Property="BorderThickness" Value="0"/>
  <Setter Property="Cursor"          Value="Hand"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="Button">
        <TextBlock x:Name="Text"
                   Text="{TemplateBinding Content}"
                   Foreground="{TemplateBinding Foreground}"
                   TextDecorations="Underline"
                   Padding="{TemplateBinding Padding}"
                   TextTrimming="CharacterEllipsis"/>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter TargetName="Text" Property="Foreground" Value="{StaticResource Tokens.Accent.500}"/>
          </Trigger>
          <Trigger Property="IsPressed" Value="True">
            <Setter TargetName="Text" Property="Foreground" Value="{StaticResource Tokens.Accent.700}"/>
          </Trigger>
          <Trigger Property="IsEnabled" Value="False">
            <Setter TargetName="Text" Property="Foreground" Value="{StaticResource Tokens.Text.Disabled}"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

#### 2.1.4 `Styles.DangerButton`

```xml
<Style x:Key="Styles.DangerButton" TargetType="Button" BasedOn="{StaticResource Styles.PrimaryButton}">
  <Setter Property="Background" Value="{StaticResource Tokens.Semantic.Error}"/>
  <Setter Property="Padding"    Value="10,6"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="Button">
        <Border x:Name="Chrome"
                Background="{TemplateBinding Background}"
                BorderThickness="0"
                CornerRadius="{StaticResource Tokens.Radius.Medium}"
                Padding="{TemplateBinding Padding}"
                SnapsToDevicePixels="True">
          <ContentPresenter HorizontalAlignment="Center"
                            VerticalAlignment="Center"
                            RecognizesAccessKey="True"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter TargetName="Chrome" Property="Background" Value="#DC2626"/>
          </Trigger>
          <Trigger Property="IsPressed" Value="True">
            <Setter TargetName="Chrome" Property="Background" Value="#B91C1C"/>
          </Trigger>
          <Trigger Property="IsEnabled" Value="False">
            <Setter TargetName="Chrome" Property="Background" Value="{StaticResource Tokens.Surface.CardActive}"/>
            <Setter Property="Foreground" Value="{StaticResource Tokens.Text.Disabled}"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

#### 2.1.5 `Styles.IconButton`

```xml
<Style x:Key="Styles.IconButton" TargetType="Button" BasedOn="{StaticResource Styles.SecondaryButton}">
  <Setter Property="MinWidth"     Value="36"/>
  <Setter Property="MinHeight"    Value="36"/>
  <Setter Property="Padding"      Value="8,6"/>
  <Setter Property="FontSize"     Value="16"/>
  <Setter Property="Content"      Value="⚙"/>
</Style>
```

#### 2.1.6 Button sizes

Apply by composition or additional keys:

| Key | Padding | FontSize | Usage |
|-----|---------|----------|-------|
| `Styles.Button.Small` | `8,4` | `12` | Compact inline actions |
| `Styles.Button.Medium` | `14,8` | `14` | Default |
| `Styles.Button.Large` | `18,12` | `16` | Wizard CTA |

Create as `Style` with `BasedOn`:

```xml
<Style x:Key="Styles.Button.Small" TargetType="Button" BasedOn="{StaticResource Styles.SecondaryButton}">
  <Setter Property="Padding" Value="8,4"/>
  <Setter Property="FontSize" Value="12"/>
</Style>
```

### 2.2 Inputs

#### 2.2.1 `Styles.ModernTextBox`

```xml
<Style x:Key="Styles.ModernTextBox" TargetType="TextBox">
  <Setter Property="Background"        Value="{StaticResource Tokens.Surface.Input}"/>
  <Setter Property="Foreground"        Value="{StaticResource Tokens.Text.Primary}"/>
  <Setter Property="CaretBrush"        Value="{StaticResource Tokens.Text.Primary}"/>
  <Setter Property="BorderBrush"       Value="{StaticResource Tokens.Divider.Default}"/>
  <Setter Property="BorderThickness"   Value="1"/>
  <Setter Property="Padding"           Value="8,6"/>
  <Setter Property="FontSize"          Value="14"/>
  <Setter Property="VerticalContentAlignment" Value="Center"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="TextBox">
        <Border x:Name="Border"
                Background="{TemplateBinding Background}"
                BorderBrush="{TemplateBinding BorderBrush}"
                BorderThickness="{TemplateBinding BorderThickness}"
                CornerRadius="{StaticResource Tokens.Radius.Medium}"
                SnapsToDevicePixels="True">
          <ScrollViewer x:Name="PART_ContentHost"
                        Margin="0"
                        Focusable="False"
                        HorizontalScrollBarVisibility="Hidden"
                        VerticalScrollBarVisibility="Hidden"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter TargetName="Border" Property="BorderBrush" Value="{StaticResource Tokens.Text.Dim}"/>
          </Trigger>
          <Trigger Property="IsKeyboardFocused" Value="True">
            <Setter TargetName="Border" Property="BorderBrush" Value="{StaticResource Tokens.Accent.500}"/>
          </Trigger>
          <Trigger Property="IsEnabled" Value="False">
            <Setter TargetName="Border" Property="Background" Value="{StaticResource Tokens.Surface.Card}"/>
            <Setter Property="Foreground" Value="{StaticResource Tokens.Text.Disabled}"/>
          </Trigger>
          <Trigger Property="Validation.HasError" Value="True">
            <Setter TargetName="Border" Property="BorderBrush" Value="{StaticResource Tokens.Semantic.Error}"/>
            <Setter TargetName="Border" Property="BorderThickness" Value="1.5"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

#### 2.2.2 `Styles.ModernPasswordBox`

Identical to `ModernTextBox` but `TargetType="PasswordBox"` and `PART_ContentHost` remains the same. WPF PasswordBox uses the same template contract.

```xml
<Style x:Key="Styles.ModernPasswordBox" TargetType="PasswordBox">
  <Setter Property="Background"        Value="{StaticResource Tokens.Surface.Input}"/>
  <Setter Property="Foreground"        Value="{StaticResource Tokens.Text.Primary}"/>
  <Setter Property="CaretBrush"        Value="{StaticResource Tokens.Text.Primary}"/>
  <Setter Property="BorderBrush"       Value="{StaticResource Tokens.Divider.Default}"/>
  <Setter Property="BorderThickness"   Value="1"/>
  <Setter Property="Padding"           Value="8,6"/>
  <Setter Property="FontSize"          Value="14"/>
  <Setter Property="VerticalContentAlignment" Value="Center"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="PasswordBox">
        <Border x:Name="Border"
                Background="{TemplateBinding Background}"
                BorderBrush="{TemplateBinding BorderBrush}"
                BorderThickness="{TemplateBinding BorderThickness}"
                CornerRadius="{StaticResource Tokens.Radius.Medium}"
                SnapsToDevicePixels="True">
          <ScrollViewer x:Name="PART_ContentHost"
                        Margin="0"
                        Focusable="False"
                        HorizontalScrollBarVisibility="Hidden"
                        VerticalScrollBarVisibility="Hidden"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter TargetName="Border" Property="BorderBrush" Value="{StaticResource Tokens.Text.Dim}"/>
          </Trigger>
          <Trigger Property="IsKeyboardFocused" Value="True">
            <Setter TargetName="Border" Property="BorderBrush" Value="{StaticResource Tokens.Accent.500}"/>
          </Trigger>
          <Trigger Property="Validation.HasError" Value="True">
            <Setter TargetName="Border" Property="BorderBrush" Value="{StaticResource Tokens.Semantic.Error}"/>
            <Setter TargetName="Border" Property="BorderThickness" Value="1.5"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

#### 2.2.3 `Styles.ModernCheckBox`

```xml
<Style x:Key="Styles.ModernCheckBox" TargetType="CheckBox">
  <Setter Property="Foreground"       Value="{StaticResource Tokens.Text.Primary}"/>
  <Setter Property="FontSize"         Value="14"/>
  <Setter Property="VerticalContentAlignment" Value="Center"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="CheckBox">
        <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
          <Border x:Name="CheckBorder"
                  Width="16" Height="16"
                  Background="{StaticResource Tokens.Surface.Input}"
                  BorderBrush="{StaticResource Tokens.Divider.Default}"
                  BorderThickness="1"
                  CornerRadius="{StaticResource Tokens.Radius.Small}"
                  Margin="0,0,8,0"
                  VerticalAlignment="Center">
            <Path x:Name="CheckMark"
                  Data="M 3 8 L 7 12 L 13 4"
                  Stroke="{StaticResource Tokens.Text.Inverse}"
                  StrokeThickness="2"
                  StrokeStartLineCap="Round"
                  StrokeEndLineCap="Round"
                  Visibility="Collapsed"
                  HorizontalAlignment="Center"
                  VerticalAlignment="Center"/>
          </Border>
          <ContentPresenter VerticalAlignment="Center" RecognizesAccessKey="True"/>
        </StackPanel>
        <ControlTemplate.Triggers>
          <Trigger Property="IsChecked" Value="True">
            <Setter TargetName="CheckBorder" Property="Background" Value="{StaticResource Tokens.Accent.500}"/>
            <Setter TargetName="CheckBorder" Property="BorderBrush" Value="{StaticResource Tokens.Accent.500}"/>
            <Setter TargetName="CheckMark" Property="Visibility" Value="Visible"/>
          </Trigger>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter TargetName="CheckBorder" Property="BorderBrush" Value="{StaticResource Tokens.Text.Dim}"/>
          </Trigger>
          <Trigger Property="IsEnabled" Value="False">
            <Setter TargetName="CheckBorder" Property="Background" Value="{StaticResource Tokens.Surface.Card}"/>
            <Setter Property="Foreground" Value="{StaticResource Tokens.Text.Disabled}"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

#### 2.2.4 `Styles.ModernComboBox`

Use a dark-themed ComboBox style that overrides the default chrome and drops the Aero popup.

```xml
<Style x:Key="Styles.ModernComboBox" TargetType="ComboBox">
  <Setter Property="Background"        Value="{StaticResource Tokens.Surface.Input}"/>
  <Setter Property="Foreground"        Value="{StaticResource Tokens.Text.Primary}"/>
  <Setter Property="BorderBrush"       Value="{StaticResource Tokens.Divider.Default}"/>
  <Setter Property="BorderThickness"   Value="1"/>
  <Setter Property="Padding"           Value="8,4"/>
  <Setter Property="FontSize"          Value="14"/>
  <Setter Property="VerticalContentAlignment" Value="Center"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="ComboBox">
        <Grid SnapsToDevicePixels="True">
          <ToggleButton x:Name="ToggleButton"
                        IsChecked="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}}"
                        ClickMode="Press"
                        Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        Padding="{TemplateBinding Padding}"
                        HorizontalContentAlignment="Stretch"
                        VerticalContentAlignment="Center">
            <ToggleButton.Template>
              <ControlTemplate TargetType="ToggleButton">
                <Border Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="{StaticResource Tokens.Radius.Medium}">
                  <Grid>
                    <ContentPresenter x:Name="ContentSite"
                                      Content="{TemplateBinding Content}"
                                      ContentTemplate="{TemplateBinding ContentTemplate}"
                                      HorizontalAlignment="Left"
                                      VerticalAlignment="Center"
                                      Margin="{TemplateBinding Padding}"/>
                    <Path Data="M 0 0 L 4 4 L 8 0"
                          Fill="{StaticResource Tokens.Text.Dim}"
                          HorizontalAlignment="Right"
                          VerticalAlignment="Center"
                          Margin="0,0,10,0"/>
                  </Grid>
                </Border>
              </ControlTemplate>
            </ToggleButton.Template>
          </ToggleButton>
          <Popup x:Name="Popup"
                 IsOpen="{TemplateBinding IsDropDownOpen}"
                 Placement="Bottom"
                 Focusable="False">
            <Border Background="{StaticResource Tokens.Surface.Elevated}"
                    BorderBrush="{StaticResource Tokens.Divider.Default}"
                    BorderThickness="1"
                    CornerRadius="{StaticResource Tokens.Radius.Medium}"
                    Effect="{StaticResource Tokens.Elevation.2}">
              <ScrollViewer>
                <ItemsPresenter SnapsToDevicePixels="True"/>
              </ScrollViewer>
            </Border>
          </Popup>
        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
  <Style.Resources>
    <Style TargetType="ComboBoxItem">
      <Setter Property="Foreground" Value="{StaticResource Tokens.Text.Primary}"/>
      <Setter Property="Padding"    Value="8,6"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="ComboBoxItem">
            <Border x:Name="ItemBorder"
                    Background="Transparent"
                    Padding="{TemplateBinding Padding}"
                    SnapsToDevicePixels="True">
              <ContentPresenter HorizontalAlignment="Left" VerticalAlignment="Center"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="ItemBorder" Property="Background" Value="{StaticResource Tokens.Surface.CardHover}"/>
              </Trigger>
              <Trigger Property="IsSelected" Value="True">
                <Setter TargetName="ItemBorder" Property="Background" Value="{StaticResource Tokens.Accent.900}"/>
                <Setter Property="Foreground" Value="{StaticResource Tokens.Accent.300}"/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Style.Resources>
</Style>
```

### 2.3 Containers

#### 2.3.1 `Styles.Card`

```xml
<Style x:Key="Styles.Card" TargetType="Border">
  <Setter Property="Background"      Value="{StaticResource Tokens.Surface.Card}"/>
  <Setter Property="BorderBrush"     Value="{StaticResource Tokens.Divider.Default}"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="CornerRadius"    Value="{StaticResource Tokens.Radius.Large}"/>
  <Setter Property="Padding"         Value="{StaticResource Tokens.Thickness.4}"/>
  <Setter Property="Effect"          Value="{StaticResource Tokens.Elevation.1}"/>
</Style>
```

Use:

```xml
<Border Style="{StaticResource Styles.Card}">
  ...
</Border>
```

#### 2.3.2 `Styles.SectionHeader`

A reusable data template / UserControl pattern. See Section 3 for the skeleton.

### 2.4 Feedback boxes

All boxes use `Border` + icon `TextBlock` + title + body.

```xml
<Style x:Key="Styles.InfoBox" TargetType="Border">
  <Setter Property="Background"      Value="{StaticResource Tokens.Semantic.InfoBg}"/>
  <Setter Property="BorderBrush"     Value="{StaticResource Tokens.Semantic.InfoBorder}"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="CornerRadius"    Value="{StaticResource Tokens.Radius.Medium}"/>
  <Setter Property="Padding"         Value="12"/>
</Style>

<Style x:Key="Styles.WarningBox" TargetType="Border">
  <Setter Property="Background"      Value="{StaticResource Tokens.Semantic.WarningBg}"/>
  <Setter Property="BorderBrush"     Value="{StaticResource Tokens.Semantic.WarningBorder}"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="CornerRadius"    Value="{StaticResource Tokens.Radius.Medium}"/>
  <Setter Property="Padding"         Value="12"/>
</Style>

<Style x:Key="Styles.ErrorBox" TargetType="Border">
  <Setter Property="Background"      Value="{StaticResource Tokens.Semantic.ErrorBg}"/>
  <Setter Property="BorderBrush"     Value="{StaticResource Tokens.Semantic.ErrorBorder}"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="CornerRadius"    Value="{StaticResource Tokens.Radius.Medium}"/>
  <Setter Property="Padding"         Value="12"/>
</Style>

<Style x:Key="Styles.SuccessBox" TargetType="Border">
  <Setter Property="Background"      Value="{StaticResource Tokens.Semantic.SuccessBg}"/>
  <Setter Property="BorderBrush"     Value="{StaticResource Tokens.Semantic.SuccessBorder}"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="CornerRadius"    Value="{StaticResource Tokens.Radius.Medium}"/>
  <Setter Property="Padding"         Value="12"/>
</Style>
```

Usage skeleton:

```xml
<Border Style="{StaticResource Styles.InfoBox}">
  <StackPanel>
    <TextBlock Text="ℹ Title" Foreground="{StaticResource Tokens.Semantic.Info}" FontWeight="SemiBold"/>
    <TextBlock Text="Body copy" Foreground="{StaticResource Tokens.Text.Secondary}" TextWrapping="Wrap"/>
  </StackPanel>
</Border>
```

### 2.5 Tabs

Subsumes the current `DarkTabItemStyle` and provides a matching `TabControl` style.

#### 2.5.1 `Styles.TabControl`

```xml
<Style x:Key="Styles.TabControl" TargetType="TabControl">
  <Setter Property="Background"      Value="Transparent"/>
  <Setter Property="BorderBrush"     Value="{StaticResource Tokens.Divider.Default}"/>
  <Setter Property="BorderThickness" Value="1"/>
</Style>
```

#### 2.5.2 `Styles.TabItem`

```xml
<Style x:Key="Styles.TabItem" TargetType="TabItem">
  <Setter Property="Background"       Value="Transparent"/>
  <Setter Property="Foreground"       Value="{StaticResource Tokens.Text.Dim}"/>
  <Setter Property="BorderBrush"      Value="Transparent"/>
  <Setter Property="Padding"          Value="14,8"/>
  <Setter Property="FontSize"         Value="13"/>
  <Setter Property="FontWeight"       Value="SemiBold"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="TabItem">
        <Border x:Name="TabBorder"
                Background="{TemplateBinding Background}"
                BorderBrush="{TemplateBinding BorderBrush}"
                BorderThickness="0,0,0,2"
                Padding="{TemplateBinding Padding}"
                SnapsToDevicePixels="True">
          <ContentPresenter ContentSource="Header"
                            HorizontalAlignment="Center"
                            VerticalAlignment="Center"
                            RecognizesAccessKey="True"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter TargetName="TabBorder" Property="Foreground" Value="{StaticResource Tokens.Text.Primary}"/>
          </Trigger>
          <Trigger Property="IsSelected" Value="True">
            <Setter TargetName="TabBorder" Property="Foreground" Value="{StaticResource Tokens.Text.Primary}"/>
            <Setter TargetName="TabBorder" Property="BorderBrush" Value="{StaticResource Tokens.Accent.500}"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

Apply:

```xml
<TabControl Style="{StaticResource Styles.TabControl}"
            ItemContainerStyle="{StaticResource Styles.TabItem}"/>
```

### 2.6 Toolbar

A pattern, not a single style: group icon buttons and label buttons in a horizontal `StackPanel` with `Spacing="{StaticResource Tokens.Space.2}"`. Use `Styles.IconButton` for glyphs and `Styles.SecondaryButton` / `Styles.PrimaryButton` for actions.

---

## 3. Patterns

Complex patterns are stored as individual XAML files under `src/OptiRoute.App/Themes/Patterns/` and merged into `Styles.xaml` via `ResourceDictionary.MergedDictionaries`.

### 3.1 Instructions panel

**File:** `src/OptiRoute.App/Themes/Patterns/InstructionsExpander.xaml`

Reusable `UserControl` with a collapsible header and numbered content. Use in all four Settings tabs.

```xml
<UserControl x:Class="OptiRoute.App.Themes.Patterns.InstructionsExpander"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Expander x:Name="RootExpander"
            Header="How to obtain this data"
            Foreground="{StaticResource Tokens.Accent.300}"
            Background="Transparent"
            IsExpanded="False"
            FontSize="13"
            FontWeight="SemiBold"
            Margin="0,12,0,0">
    <Expander.HeaderTemplate>
      <DataTemplate>
        <TextBlock Text="{Binding}" Foreground="{StaticResource Tokens.Accent.300}"/>
      </DataTemplate>
    </Expander.HeaderTemplate>
    <Border Background="{StaticResource Tokens.Surface.Card}"
            BorderBrush="{StaticResource Tokens.Divider.Default}"
            BorderThickness="1"
            CornerRadius="{StaticResource Tokens.Radius.Medium}"
            Padding="12"
            Margin="0,8,0,0">
      <StackPanel x:Name="ContentPanel">
        <!-- Consumers add numbered TextBlocks here -->
      </StackPanel>
    </Border>
  </Expander>
</UserControl>
```

Usage:

```xml
<local:InstructionsExpander>
  <StackPanel>
    <TextBlock Text="1. Open OPNsense → System → Access → API Keys." TextWrapping="Wrap" Foreground="{StaticResource Tokens.Text.Secondary}" Margin="0,0,0,4"/>
    <TextBlock Text="2. Click '+' to generate a new key pair."        TextWrapping="Wrap" Foreground="{StaticResource Tokens.Text.Secondary}" Margin="0,0,0,4"/>
    <TextBlock Text="3. Download the .txt file and import below."     TextWrapping="Wrap" Foreground="{StaticResource Tokens.Text.Secondary}"/>
  </StackPanel>
</local:InstructionsExpander>
```

### 3.2 Validation pattern

#### 3.2.1 Per-field: `Patterns.ValidationField`

Wrap the input + error text in a `UserControl` / template so every field follows the same structure.

```xml
<UserControl x:Class="OptiRoute.App.Themes.Patterns.ValidationField"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <StackPanel>
    <ContentControl x:Name="InputHost" Focusable="False"/>
    <TextBlock x:Name="ErrorBlock"
               Foreground="{StaticResource Tokens.Semantic.Error}"
               FontSize="12"
               Margin="0,4,0,0"
               TextWrapping="Wrap"
               Visibility="Collapsed"/>
  </StackPanel>
</UserControl>
```

Attach behavior via a small code-behind helper that watches `Validation.HasError` on the child element. No custom markup extension is needed.

#### 3.2.2 Window-level: `Patterns.ValidationSummary`

```xml
<Border Style="{StaticResource Styles.ErrorBox}"
        Visibility="{Binding HasErrors, Converter={StaticResource BoolToVis}}">
  <StackPanel>
    <TextBlock Text="Please fix the following errors:" FontWeight="SemiBold" Foreground="{StaticResource Tokens.Semantic.Error}"/>
    <ItemsControl ItemsSource="{Binding ValidationErrors}"
                  Foreground="{StaticResource Tokens.Text.Secondary}">
      <ItemsControl.ItemTemplate>
        <DataTemplate>
          <TextBlock Text="• {Binding}" TextWrapping="Wrap"/>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>
  </StackPanel>
</Border>
```

`SaveCommand.CanExecute` returns `false` when `HasErrors` is true.

### 3.3 Empty state

Use in `MainWindow` and any future empty list screen.

```xml
<Border Style="{StaticResource Styles.Card}"
        Padding="{StaticResource Tokens.Thickness.6}"
        VerticalAlignment="Center"
        HorizontalAlignment="Center">
  <StackPanel HorizontalAlignment="Center">
    <TextBlock Text="🎮" FontSize="44" HorizontalAlignment="Center" Margin="0,0,0,{StaticResource Tokens.Space.3}"/>
    <TextBlock Text="{x:Static p:Strings.MainWindow_EmptyState_Title}"
               Style="{StaticResource Styles.Text.H2}"
               HorizontalAlignment="Center"
               Foreground="{StaticResource Tokens.Text.Primary}"/>
    <TextBlock Text="{x:Static p:Strings.MainWindow_EmptyState_Subtitle}"
               FontSize="13"
               Foreground="{StaticResource Tokens.Text.Dim}"
               TextWrapping="Wrap"
               TextAlignment="Center"
               Margin="0,{StaticResource Tokens.Space.2},0,{StaticResource Tokens.Space.4}"/>
    <Button Content="{x:Static p:Strings.MainWindow_EmptyState_AddButton}"
            Command="{Binding AddApplicationCommand}"
            Style="{StaticResource Styles.PrimaryButton}"
            HorizontalAlignment="Center"/>
  </StackPanel>
</Border>
```

### 3.4 Loading stages

Status text in footer / progress overlay follows a textual progress model:

```
Status.Connecting          → "Connecting to OPNsense and detecting network…"
Status.Refreshing          → "Refreshing…"
Status.SyncProgress.Ready  → "Reading OPNsense rules…"
Status.SyncProgress.Rules  → "Reading OPNsense rules…"
Status.SyncProgress.Qos    → "Reading Windows QoS policies…"
Status.SyncProgress.Done   → "Comparing configuration…"
```

UI: footer `TextBlock` bound to `StatusMessage` plus a thin `ProgressBar` visible while `IsLoading` is true.

### 3.5 Modal pattern

Base modal window template:

```xml
<Window ...
        ShowInTaskbar="False"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize"
        Background="{StaticResource Tokens.Surface.Window}"
        Foreground="{StaticResource Tokens.Text.Primary}"
        MinWidth="420">
  <Grid Margin="{StaticResource Tokens.Thickness.5}">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <!-- Header -->
    <TextBlock Grid.Row="0"
               Text="{x:Static p:Strings.Dialog_InvalidTos_Title}"
               Style="{StaticResource Styles.Text.H2}"
               Margin="0,0,0,{StaticResource Tokens.Space.3}"/>

    <!-- Body -->
    <ContentControl Grid.Row="1" x:Name="BodyHost" Margin="0,0,0,{StaticResource Tokens.Space.4}"/>

    <!-- Footer -->
    <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right">
      <Button Content="{x:Static p:Strings.Common_Cancel}"
              Style="{StaticResource Styles.SecondaryButton}"
              Margin="0,0,{StaticResource Tokens.Space.2},0"
              IsCancel="True"/>
      <Button Content="{x:Static p:Strings.Common_Save}"
              Style="{StaticResource Styles.PrimaryButton}"
              IsDefault="True"/>
    </StackPanel>
  </Grid>
</Window>
```

---

## 4. Implementation Strategy

### 4.1 Where

Create one central resource dictionary:

```
src/OptiRoute.App/Themes/Styles.xaml
```

Complex patterns (UserControls) go in:

```
src/OptiRoute.App/Themes/Patterns/
  InstructionsExpander.xaml
  ValidationField.xaml
  ValidationSummary.xaml
```

### 4.2 How to load

Update `App.xaml` to merge the dictionary at application level:

```xml
<Application x:Class="OptiRoute.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:OptiRoute.App">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="Themes/Styles.xaml"/>
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

### 4.3 Access

Reference tokens and styles with `{StaticResource ...}` in every window:

```xml
<Button Style="{StaticResource Styles.PrimaryButton}"
        Background="{StaticResource Tokens.Accent.500}"/>
```

Use **only** `StaticResource`. No `DynamicResource`.

### 4.4 Naming convention

| Prefix | Meaning | Example |
|--------|---------|---------|
| `Tokens.*` | Primitive value (color, size, duration) | `Tokens.Accent.500` |
| `Styles.*` | Reusable control style | `Styles.PrimaryButton` |
| `Patterns.*` | Composite UserControl / template | `Patterns.InstructionsExpander` |

### 4.5 Copy stays in resources

All user-facing strings remain in:

- `src/OptiRoute.App/Strings.resx`
- `src/OptiRoute.App/Strings.pt-BR.resx`
- `src/OptiRoute.App/Strings.Designer.cs`

Styles must not contain literal text. Use `{x:Static p:Strings.X}` in XAML only.

### 4.6 No new packages

Implementation is pure WPF XAML + existing project references. Do not add new NuGet packages.

---

## 5. Migration Plan

### 5.1 New files

| File | Purpose |
|------|---------|
| `src/OptiRoute.App/Themes/Styles.xaml` | All tokens + component styles |
| `src/OptiRoute.App/Themes/Patterns/InstructionsExpander.xaml` | Collapsible how-to panel |
| `src/OptiRoute.App/Themes/Patterns/ValidationField.xaml` | Input + error wrapper |
| `src/OptiRoute.App/Themes/Patterns/ValidationSummary.xaml` | Window-level error summary |

### 5.2 Files that change

| File | Change |
|------|--------|
| `src/OptiRoute.App/App.xaml` | Add merged `Themes/Styles.xaml` dictionary |
| `src/OptiRoute.App/Windows/SettingsWindow.xaml` | Remove inline `Background`/`Foreground`; use styles; replace `DarkTabItemStyle` with `Styles.TabItem`; replace Welcome `Border` with token colors; apply input styles; add `InstructionsExpander` |
| `src/OptiRoute.App/MainWindow.xaml` | Remove inline `Window.Resources` brushes and inline button styles; use `Styles.*`; replace inline card Borders with `Styles.Card`; clean footer / empty state |

### 5.3 Inline style → token/style mapping

| Current inline value | New token/style |
|----------------------|-----------------|
| `Background="#18181B"` | `Background="{StaticResource Tokens.Surface.Window}"` |
| `Foreground="#F4F4F5"` | `Foreground="{StaticResource Tokens.Text.Primary}"` |
| `Background="#27272A"` | `Background="{StaticResource Tokens.Surface.Card}"` |
| `BorderBrush="#3F3F46"` | `BorderBrush="{StaticResource Tokens.Divider.Default}"` |
| `Foreground="#A1A1AA"` | `Foreground="{StaticResource Tokens.Text.Dim}"` |
| `Background="#3B82F6"` | `Style="{StaticResource Styles.PrimaryButton}"` |
| `Background="#3F3F46" Foreground="#F4F4F5"` | `Style="{StaticResource Styles.SecondaryButton}"` |
| `Background="#EF4444"` | `Style="{StaticResource Styles.DangerButton}"` |
| `Background="#1E3A8A"` | `Background="{StaticResource Tokens.Accent.900}"` |
| `Foreground="#93C5FD"` | `Foreground="{StaticResource Tokens.Accent.300}"` |
| `Foreground="#60A5FA"` | `Foreground="{StaticResource Tokens.Semantic.Info}"` |
| `Foreground="#10B981"` | `Foreground="{StaticResource Tokens.Semantic.Success}"` |
| `DarkTabItemStyle` | `Styles.TabItem` |
| `ModernButton` | `Styles.PrimaryButton` |
| `SecondaryButton` | `Styles.SecondaryButton` |
| `DangerButton` | `Styles.DangerButton` |

### 5.4 Implementation order

1. **Create** `Themes/Styles.xaml` with all tokens and component styles.
2. **Wire** `App.xaml` to load `Styles.xaml`.
3. **Migrate** `SettingsWindow.xaml`:
   - Remove `Window.Resources` colors.
   - Replace `DarkTabItemStyle` with `Styles.TabItem`.
   - Apply `Styles.PrimaryButton` / `Styles.SecondaryButton` to all buttons.
   - Apply `Styles.ModernTextBox`, `Styles.ModernPasswordBox`, `Styles.ModernCheckBox`, `Styles.ModernComboBox` to inputs.
   - Recolor Welcome Border using `Tokens.Accent.900` / `Tokens.Semantic.Info`.
   - Add `InstructionsExpander` to Connection, Credentials, Gateways, Advanced tabs.
4. **Migrate** `MainWindow.xaml`:
   - Remove `Window.Resources` and inline button styles.
   - Apply `Styles.PrimaryButton`, `Styles.SecondaryButton`, `Styles.DangerButton`, `Styles.IconButton`.
   - Replace card Borders with `Styles.Card`.
   - Use semantic tokens for colored panels (LocalOnly, GlobalOnly, Conflict).
   - Use empty-state pattern.
5. **Build** and verify visual parity.
6. **Add** future patterns (ValidationSummary, ValidationField) as needed.

### 5.5 Risk: ControlTemplate validation trigger

The `Validation.HasError` trigger in the custom `TextBox` / `PasswordBox` templates depends on validation rules in the view model. If a field uses `IDataErrorInfo` / `ValidationRule`, the red border appears automatically. If not, keep fallback manual `BorderBrush="{StaticResource Tokens.Semantic.Error}"` in the per-field wrapper.

---

## 6. Acceptance criteria

- [ ] `Styles.xaml` exists and is merged in `App.xaml`.
- [ ] Every button uses a `Styles.*` style with a custom `ControlTemplate` (no white default chrome).
- [ ] No inline `Background`/`Foreground` hex values remain in `SettingsWindow.xaml` or `MainWindow.xaml`.
- [ ] `TabControl` uses `Styles.TabItem` across both windows consistently.
- [ ] `InstructionsExpander` is present in all four Settings tabs.
- [ ] All strings remain in `Strings.resx` / `Strings.pt-BR.resx`; no literal copy in styles.
- [ ] App builds and starts without adding NuGet packages.
