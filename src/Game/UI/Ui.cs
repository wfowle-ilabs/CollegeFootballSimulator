using Godot;

namespace CollegeFootballSimGoDot.UI;

/// <summary>Factory helpers + palette for the code-driven UI (cards, colored panels, buttons).</summary>
public static class Ui
{
	// Palette ("stadium" dark theme).
	public static readonly Color Bg = new("12161d");
	public static readonly Color Panel = new("1b2230");
	public static readonly Color PanelBorder = new("2d3a4e");
	public static readonly Color Accent = new("4cc38a");   // field green
	public static readonly Color Accent2 = new("e0a73a");  // gold
	public static readonly Color Text = new("dfe6ee");
	public static readonly Color Muted = new("8aa0b4");

	public static Label Label(string text, int size = 16, Color? color = null)
	{
		var label = new Label { Text = text };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color ?? Text);
		return label;
	}

	public static Label Heading(string text) => Label(text, 28, Accent);

	public static Button Button(string text, Action onPressed, bool primary = false)
	{
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(120, 34) };
		button.AddThemeStyleboxOverride("normal", ButtonStyle(primary ? Accent : Panel));
		button.AddThemeStyleboxOverride("hover", ButtonStyle(primary ? Accent.Lightened(0.1f) : Panel.Lightened(0.12f)));
		button.AddThemeStyleboxOverride("pressed", ButtonStyle(primary ? Accent.Darkened(0.1f) : Panel.Darkened(0.1f)));
		button.AddThemeColorOverride("font_color", primary ? Bg : Text);
		button.Pressed += onPressed;
		return button;
	}

	/// <summary>A styled dropdown button (its <see cref="Godot.PopupMenu"/> is filled by the caller).</summary>
	public static MenuButton MenuButton(string text)
	{
		var mb = new MenuButton { Text = text, CustomMinimumSize = new Vector2(150, 34), Flat = false };
		mb.AddThemeStyleboxOverride("normal", ButtonStyle(Panel));
		mb.AddThemeStyleboxOverride("hover", ButtonStyle(Panel.Lightened(0.12f)));
		mb.AddThemeStyleboxOverride("pressed", ButtonStyle(Panel.Darkened(0.1f)));
		mb.AddThemeColorOverride("font_color", Text);
		return mb;
	}

	public static VBoxContainer VBox(int separation = 8)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", separation);
		return box;
	}

	public static HBoxContainer HBox(int separation = 8)
	{
		var box = new HBoxContainer();
		box.AddThemeConstantOverride("separation", separation);
		return box;
	}

	/// <summary>A titled "widget" card wrapping content with a colored panel + border.</summary>
	public static Control Card(string title, Control content)
	{
		var panel = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		panel.AddThemeStyleboxOverride("panel", CardStyle());

		var v = VBox(6);
		v.AddChild(Label(title, 15, Accent));
		content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		v.AddChild(content);
		panel.AddChild(v);
		return panel;
	}

	/// <summary>A card containing a scrollable text body. Returns the card and the body label.</summary>
	public static (Control Panel, Label Body) ScrollText(string title)
	{
		var body = Label("", 13);
		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		scroll.AddChild(body);
		return (Card(title, scroll), body);
	}

	/// <summary>A card containing a scrollable VBox you can fill with arbitrary rows (e.g. buttons).</summary>
	public static (Control Panel, VBoxContainer Body) ScrollBox(string title)
	{
		var body = VBox(3);
		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		scroll.AddChild(body);
		return (Card(title, scroll), body);
	}

	/// <summary>A small rounded badge (e.g. a network or time-slot tag).</summary>
	public static Control Chip(string text, Color? bg = null, Color? fg = null)
	{
		var panel = new PanelContainer();
		var style = new StyleBoxFlat
		{
			BgColor = bg ?? PanelBorder,
			CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
			ContentMarginLeft = 7, ContentMarginRight = 7,
			ContentMarginTop = 2, ContentMarginBottom = 2,
		};
		panel.AddThemeStyleboxOverride("panel", style);
		panel.AddChild(Label(text, 11, fg ?? Text));
		panel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		return panel;
	}

	/// <summary>A plain bordered tile (a card without the accent title). Caller fills the body.
	/// <paramref name="highlight"/> draws an accent border (e.g. the current day).</summary>
	public static PanelContainer Tile(bool highlight = false)
	{
		var panel = new PanelContainer();
		StyleBoxFlat style = CardStyle();
		if (highlight)
		{
			style.BorderColor = Accent;
			style.SetBorderWidthAll(2);
			style.BgColor = Panel.Lightened(0.04f);
		}
		panel.AddThemeStyleboxOverride("panel", style);
		return panel;
	}

	/// <summary>A compact, accent-colored text link button (breadcrumbs, inline links).</summary>
	public static Button LinkButton(string text, Action onPressed, Color? color = null)
	{
		var button = new Button { Text = text, Flat = true };
		button.AddThemeColorOverride("font_color", color ?? Accent);
		button.AddThemeColorOverride("font_hover_color", (color ?? Accent).Lightened(0.25f));
		button.Pressed += onPressed;
		return button;
	}

	/// <summary>A full-width, left-aligned row button (for clickable list items).</summary>
	public static Button RowButton(string text, Action onPressed)
	{
		var button = new Button { Text = text, Alignment = HorizontalAlignment.Left, Flat = true };
		button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		button.AddThemeColorOverride("font_color", Text);
		button.AddThemeColorOverride("font_hover_color", Accent);
		button.Pressed += onPressed;
		return button;
	}

	public static CenterContainer Centered(Control child)
	{
		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		center.AddChild(child);
		return center;
	}

	public static MarginContainer Padded(Control child, int margin = 24)
	{
		var m = new MarginContainer();
		m.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
			m.AddThemeConstantOverride(side, margin);
		m.AddChild(child);
		return m;
	}

	public static ColorRect Background()
	{
		var rect = new ColorRect { Color = Bg };
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		return rect;
	}

	/// <summary>An opaque panel for the slide-over menu (solid background + accent left edge).</summary>
	public static StyleBoxFlat SidePanelStyle()
	{
		var s = new StyleBoxFlat
		{
			BgColor = Panel,
			BorderColor = Accent,
			ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 4, ContentMarginBottom = 4,
		};
		s.BorderWidthLeft = 2;
		return s;
	}

	private static StyleBoxFlat CardStyle()
	{
		var s = new StyleBoxFlat
		{
			BgColor = Panel,
			BorderColor = PanelBorder,
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
			ContentMarginLeft = 12, ContentMarginRight = 12,
			ContentMarginTop = 10, ContentMarginBottom = 10,
		};
		s.SetBorderWidthAll(1);
		return s;
	}

	private static StyleBoxFlat ButtonStyle(Color bg)
	{
		var s = new StyleBoxFlat
		{
			BgColor = bg,
			CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5,
			CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
			ContentMarginLeft = 12, ContentMarginRight = 12,
			ContentMarginTop = 6, ContentMarginBottom = 6,
		};
		return s;
	}
}
