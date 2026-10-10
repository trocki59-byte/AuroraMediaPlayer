using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace AuroraMediaPlayer.Views;

/// <summary>
/// Üst menüdeki cam kart sekme butonu (Ana Sayfa, Videolar, Müzikler…).
/// Seçili olduğunda aurora gradyan kenarlık ve parlama alır.
/// </summary>
public sealed class NavTabButton : ContentView
{
	public static readonly BindableProperty IconProperty =
		BindableProperty.Create(nameof(Icon), typeof(string), typeof(NavTabButton), "",
			propertyChanged: (b, _, n) => ((NavTabButton)b)._icon.Source = (string)n);

	public static readonly BindableProperty TextProperty =
		BindableProperty.Create(nameof(Text), typeof(string), typeof(NavTabButton), "",
			propertyChanged: (b, _, n) => ((NavTabButton)b)._label.Text = (string)n);

	public static readonly BindableProperty IsSelectedProperty =
		BindableProperty.Create(nameof(IsSelected), typeof(bool), typeof(NavTabButton), false,
			propertyChanged: (b, _, _) => ((NavTabButton)b).UpdateVisual());

	public static readonly BindableProperty CommandProperty =
		BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(NavTabButton));

	public static readonly BindableProperty CommandParameterProperty =
		BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(NavTabButton));

	/// <summary>Ayarlar butonu gibi mor vurgulu seçili kenarlık.</summary>
	public static readonly BindableProperty UsePurpleAccentProperty =
		BindableProperty.Create(nameof(UsePurpleAccent), typeof(bool), typeof(NavTabButton), false,
			propertyChanged: (b, _, _) => ((NavTabButton)b).UpdateVisual());

	readonly Border _card;
	readonly Image _icon;
	readonly Label _label;
	readonly Grid _layout;

	public NavTabButton()
	{
		_icon = new Image
		{
			HeightRequest = 34,
			WidthRequest = 34,
			Aspect = Aspect.AspectFit,
			HorizontalOptions = LayoutOptions.Center,
		};

		_label = new Label
		{
			FontFamily = "PoppinsMedium",
			FontSize = 13,
			TextColor = Colors.White,
			HorizontalTextAlignment = TextAlignment.Center,
			LineBreakMode = LineBreakMode.TailTruncation,
			MaxLines = 1,
		};

		_layout = new Grid
		{
			RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)),
			RowSpacing = 4,
			Padding = new Thickness(6, 10, 6, 8),
		};
		_layout.Add(_icon, 0, 0);
		_layout.Add(_label, 0, 1);

		_card = new Border
		{
			StrokeShape = new RoundRectangle { CornerRadius = 16 },
			StrokeThickness = 1.2,
			Content = _layout,
			MinimumHeightRequest = 86,
		};

		var tap = new TapGestureRecognizer();
		tap.Tapped += OnTapped;
		_card.GestureRecognizers.Add(tap);

		Content = _card;
		UpdateVisual();
	}

	public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
	public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
	public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
	public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
	public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
	public bool UsePurpleAccent { get => (bool)GetValue(UsePurpleAccentProperty); set => SetValue(UsePurpleAccentProperty, value); }

	public void SetCompact(bool compact)
	{
		_icon.HeightRequest = _icon.WidthRequest = compact ? 26 : 34;
		_label.FontSize = compact ? 11.5 : 13;
		_card.MinimumHeightRequest = compact ? 70 : 86;
		_layout.Padding = compact ? new Thickness(4, 8, 4, 6) : new Thickness(6, 10, 6, 8);
	}

	async void OnTapped(object? sender, TappedEventArgs e)
	{
		await _card.ScaleToAsync(0.94, 70, Easing.CubicOut);
		await _card.ScaleToAsync(1.0, 110, Easing.CubicOut);
		if (Command?.CanExecute(CommandParameter) == true)
			Command.Execute(CommandParameter);
	}

	void UpdateVisual()
	{
		if (IsSelected)
		{
			var glow = UsePurpleAccent ? Color.FromArgb("#C04BFF") : Color.FromArgb("#2FB8FF");
			_card.Background = UsePurpleAccent
				? new LinearGradientBrush(
					[new GradientStop(Color.FromArgb("#E6281A6E"), 0), new GradientStop(Color.FromArgb("#E6101040"), 1)],
					new Point(0, 0), new Point(0, 1))
				: (Brush)Application.Current!.Resources["SelectedTabFill"];
			_card.Stroke = UsePurpleAccent
				? new LinearGradientBrush(
					[new GradientStop(Color.FromArgb("#FF4BD8"), 0), new GradientStop(Color.FromArgb("#2FE3FF"), 1)],
					new Point(0, 0), new Point(1, 1))
				: new LinearGradientBrush(
					[new GradientStop(Color.FromArgb("#7FF2FF"), 0), new GradientStop(Color.FromArgb("#3B7BFF"), 1)],
					new Point(0, 0), new Point(1, 1));
			_card.StrokeThickness = 2.2;
			_card.Shadow = new Shadow { Brush = new SolidColorBrush(glow), Opacity = 0.85f, Radius = 18, Offset = new Point(0, 0) };
			_label.FontFamily = "PoppinsBold";
		}
		else
		{
			_card.Background = Application.Current?.Resources.TryGetValue("TabFill", out var fill) == true
				? (Brush)fill
				: new SolidColorBrush(Color.FromArgb("#CC0E1840"));
			_card.Stroke = new SolidColorBrush(Color.FromArgb("#335B8CFF"));
			_card.StrokeThickness = 1.2;
			_card.Shadow = null!;
			_label.FontFamily = "PoppinsMedium";
		}
	}
}
