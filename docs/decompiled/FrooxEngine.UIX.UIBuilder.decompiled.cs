using System;
using System.Collections.Generic;
using Elements.Core;
using Elements.Data;
using Elements.Quantity;
using Renderite.Shared;

namespace FrooxEngine.UIX;

[DataModelType]
public class UIBuilder
{
	private Stack<Slot> roots = new Stack<Slot>();

	private Stack<UIStyle> _uiStyles = new Stack<UIStyle>();

	private bool rootIsLayout;

	private IAssetProvider<Sprite> _checkSprite;

	private IAssetProvider<Sprite> _circleSprite;

	public World World => Canvas.World;

	public Canvas Canvas { get; private set; }

	public Slot Root => roots.Peek();

	public Slot Current { get; private set; }

	public Slot LayoutTarget { get; set; }

	public bool IsAtRoot => roots.Count <= 1;

	public RectTransform CurrentRect => Current?.GetComponent<RectTransform>() ?? Root.GetComponent<RectTransform>();

	public UIStyle Style => _uiStyles.Peek();

	public RectTransform ForceNext { get; set; }

	public IAssetProvider<Sprite> CheckSprite
	{
		get
		{
			if (_checkSprite == null)
			{
				_checkSprite = Root.World.GetSharedComponentOrCreate("BasicUI_Check", delegate(SpriteProvider sprite)
				{
					StaticTexture2D target = Root.World.RootSlot.AttachTexture(OfficialAssets.Common.Icons.Check, getExisting: true, uncompressed: true, directLoad: false, evenNull: false, TextureWrapMode.Clamp);
					sprite.Texture.Target = target;
				}, 1, replaceExisting: true, updateExisting: true);
			}
			return _checkSprite;
		}
	}

	public IAssetProvider<Sprite> CircleSprite
	{
		get
		{
			if (_circleSprite == null)
			{
				_circleSprite = GetCircleSprite(World);
			}
			return _circleSprite;
		}
	}

	public void PushStyle()
	{
		_uiStyles.Push(Style.Clone());
	}

	public void PopStyle()
	{
		_uiStyles.Pop();
	}

	public static IAssetProvider<ITexture2D> GetCircleTexture(World world)
	{
		return world.RootSlot.AttachTexture(OfficialAssets.Common.Particles.Disc, getExisting: true, uncompressed: true, directLoad: false, evenNull: false, TextureWrapMode.Clamp);
	}

	public static IAssetProvider<Sprite> GetCircleSprite(World world)
	{
		return world.GetSharedComponentOrCreate("BasicUI_Circle", delegate(SpriteProvider sprite)
		{
			sprite.Texture.Target = GetCircleTexture(world);
		}, 0, replaceExisting: true, updateExisting: true);
	}

	public UIBuilder(RectTransform rect)
		: this(rect.Slot)
	{
	}

	public UIBuilder(Canvas canvas)
		: this(canvas.Slot)
	{
	}

	public UIBuilder(Slot root, Slot forceNext = null)
	{
		ForceNext = forceNext?.GetComponentOrAttach<RectTransform>();
		Canvas = root.GetComponent<Canvas>();
		if (Canvas == null)
		{
			root.GetComponentOrAttach<RectTransform>();
			Canvas = root.GetComponentInParents<Canvas>();
		}
		_uiStyles.Push(new UIStyle());
		roots.Push(root);
		Update();
	}

	public UIBuilder(Slot root, float canvasWidth, float canvasHeight, float canvasScale)
	{
		_uiStyles.Push(new UIStyle());
		Canvas = root.AttachComponent<Canvas>();
		Canvas.Size.Value = new float2(canvasWidth, canvasHeight);
		root.LocalScale = float3.One * canvasScale;
		roots.Push(root);
	}

	public void Nest()
	{
		LayoutTarget = null;
		if (Current == null)
		{
			throw new Exception("No Current element to nest into!");
		}
		if (Current != Root)
		{
			roots.Push(Current);
		}
		Current = null;
		Update();
	}

	public void NestInto(Slot slot)
	{
		NestInto(slot.GetComponentOrAttach<RectTransform>());
	}

	public void NestInto(RectTransform root)
	{
		LayoutTarget = null;
		roots.Push(root.Slot);
		Current = null;
		Update();
	}

	public void NestOut()
	{
		LayoutTarget = null;
		if (!IsAtRoot)
		{
			Current = roots.Pop();
			Update();
			return;
		}
		throw new InvalidOperationException("No more roots to pop, at the top of the canvas. Canvas:\n" + Canvas);
	}

	public void NestOutFrom(RectTransform rect)
	{
		NestOutFrom(rect.Slot);
	}

	public void NestOutFrom(Slot root)
	{
		while (roots.Count > 0 && Root != root)
		{
			NestOut();
		}
		if (roots.Count > 0)
		{
			NestOut();
		}
	}

	public Slot Next(string name)
	{
		LayoutTarget = null;
		if (ForceNext != null)
		{
			Current = ForceNext.Slot;
			ForceNext = null;
		}
		else
		{
			Current = Root.AddSlot(name);
			Current.GetComponentOrAttach<RectTransform>();
		}
		if (rootIsLayout && !Style.SupressLayoutElement)
		{
			SetupCurrentAsLayoutElement();
		}
		return Current;
	}

	private void SetupCurrentAsLayoutElement()
	{
		LayoutElement layoutElement = Current.AttachComponent<LayoutElement>();
		layoutElement.MinWidth.Value = Style.MinWidth;
		layoutElement.MinHeight.Value = Style.MinHeight;
		layoutElement.PreferredWidth.Value = Style.PreferredWidth;
		layoutElement.PreferredHeight.Value = Style.PreferredHeight;
		layoutElement.FlexibleWidth.Value = Style.FlexibleWidth;
		layoutElement.FlexibleHeight.Value = Style.FlexibleHeight;
		layoutElement.UseZeroMetrics.Value = Style.UseZeroMetrics;
	}

	private void NextForLayout(string name)
	{
		if (LayoutTarget != null)
		{
			Current = LayoutTarget;
		}
		else
		{
			Next(name);
		}
	}

	private void Update()
	{
		rootIsLayout = Root.GetComponent<HorizontalLayout>() != null || Root.GetComponent<VerticalLayout>() != null || Root.GetComponent<GridLayout>() != null || Root.GetComponent<OverlappingLayout>() != null;
	}

	public Text Text(in LocaleString text, bool bestFit = true, Alignment? alignment = null, bool parseRTF = true, string nullContent = null)
	{
		Next("Text");
		Text text2 = Current.AttachComponent<Text>();
		if (Style.Font != null)
		{
			text2.Font.Target = Style.Font;
		}
		text2.LocaleContent = text;
		text2.NullContent.Value = nullContent;
		text2.ParseRichText.Value = parseRTF;
		text2.Color.Value = Style.TextColor;
		text2.AutoSize = bestFit;
		text2.AutoSizeMin.Value = Style.TextAutoSizeMin;
		text2.AutoSizeMax.Value = Style.TextAutoSizeMax;
		text2.LineHeight.Value = Style.TextLineHeight;
		text2.Align = alignment ?? Style.TextAlignment;
		return text2;
	}

	public Text Text(in LocaleString text, int size, bool bestFit = true, Alignment? alignment = null, bool parseRTF = true)
	{
		Text text2 = Text(in text, bestFit, alignment, parseRTF);
		text2.Size.Value = size;
		return text2;
	}

	public Slot Empty(string name = "Slot")
	{
		Next(name);
		return Current;
	}

	/// <summary>
	/// Creates a panel which will nest subsequent UI elements until a matching NestOut call is made.
	/// </summary>
	/// <returns></returns>
	public RectTransform Panel()
	{
		Next("Panel");
		Nest();
		return Root.GetComponent<RectTransform>();
	}

	public Image Panel(in colorX tint, bool zwrite = false)
	{
		return Panel(in tint, null, NineSliceSizing.FixedSize, zwrite);
	}

	public Image Panel(in colorX tint, IAssetProvider<Sprite> sprite, NineSliceSizing sizing = NineSliceSizing.FixedSize, bool zwrite = false)
	{
		Image image = Image(in tint, zwrite);
		if (sprite != null)
		{
			image.Sprite.Target = sprite;
			image.NineSliceSizing.Value = sizing;
		}
		Nest();
		return image;
	}

	public RectTransform Spacer(float size)
	{
		float minWidth = Style.MinWidth;
		float preferredWidth = Style.PreferredWidth;
		float flexibleWidth = Style.FlexibleWidth;
		float minHeight = Style.MinHeight;
		float preferredHeight = Style.PreferredHeight;
		float flexibleHeight = Style.FlexibleHeight;
		Style.Width = size;
		Style.Height = size;
		Style.FlexibleHeight = -1f;
		Style.FlexibleWidth = -1f;
		Empty("Spacer");
		Style.MinWidth = minWidth;
		Style.PreferredWidth = preferredWidth;
		Style.FlexibleWidth = flexibleWidth;
		Style.MinHeight = minHeight;
		Style.PreferredHeight = preferredHeight;
		Style.FlexibleHeight = flexibleHeight;
		return CurrentRect;
	}

	public List<RectTransform> SplitHorizontally(params float[] proportions)
	{
		MathX.NormalizeSum(proportions);
		List<RectTransform> list = new List<RectTransform>();
		float num = 0f;
		foreach (float num2 in proportions)
		{
			RectTransform component = Empty("Split").GetComponent<RectTransform>();
			component.AnchorMin.Value = new float2(num);
			component.AnchorMax.Value = new float2(num + num2, 1f);
			num += num2;
			list.Add(component);
		}
		return list;
	}

	public List<RectTransform> SplitVertically(params float[] proportions)
	{
		MathX.NormalizeSum(proportions);
		List<RectTransform> list = new List<RectTransform>();
		float num = 1f;
		foreach (float num2 in proportions)
		{
			RectTransform component = Empty("Split").GetComponent<RectTransform>();
			component.AnchorMin.Value = new float2(0f, num - num2);
			component.AnchorMax.Value = new float2(1f, num);
			num -= num2;
			list.Add(component);
		}
		return list;
	}

	public void SplitVertically(float proportion, out RectTransform top, out RectTransform bottom, float gap = 0f)
	{
		proportion = 1f - proportion;
		float num = gap * 0.5f;
		top = Empty("Top").GetComponent<RectTransform>();
		bottom = Empty("Bottom").GetComponent<RectTransform>();
		top.AnchorMin.Value = new float2(0f, proportion + num);
		top.AnchorMax.Value = new float2(1f, 1f);
		bottom.AnchorMin.Value = new float2(0f, 0f);
		bottom.AnchorMax.Value = new float2(1f, proportion - num);
	}

	public void SplitHorizontally(float proportion, out RectTransform left, out RectTransform right, float gap = 0f)
	{
		float num = gap * 0.5f;
		left = Empty("Left").GetComponent<RectTransform>();
		right = Empty("Right").GetComponent<RectTransform>();
		left.AnchorMin.Value = new float2(0f, 0f);
		left.AnchorMax.Value = new float2(proportion - num, 1f);
		right.AnchorMin.Value = new float2(proportion + num);
		right.AnchorMax.Value = new float2(1f, 1f);
	}

	public void HorizontalHeader(float size, out RectTransform header, out RectTransform content)
	{
		header = Empty("Header").GetComponent<RectTransform>();
		content = Empty("Content").GetComponent<RectTransform>();
		header.OffsetMin.Value = new float2(0f, 0f - size);
		header.AnchorMin.Value = new float2(0f, 1f);
		header.AnchorMax.Value = new float2(1f, 1f);
		content.OffsetMax.Value = new float2(0f, 0f - size);
	}

	public void HorizontalFooter(float size, out RectTransform footer, out RectTransform content)
	{
		content = Empty("Content").GetComponent<RectTransform>();
		footer = Empty("Footer").GetComponent<RectTransform>();
		footer.OffsetMax.Value = new float2(0f, size);
		footer.AnchorMin.Value = new float2(0f, 0f);
		footer.AnchorMax.Value = new float2(1f);
		content.OffsetMin.Value = new float2(0f, size);
	}

	public void VerticalHeader(float size, out RectTransform header, out RectTransform content)
	{
		header = Empty("Header").GetComponent<RectTransform>();
		content = Empty("Content").GetComponent<RectTransform>();
		header.OffsetMax.Value = new float2(size);
		header.AnchorMin.Value = new float2(0f, 0f);
		header.AnchorMax.Value = new float2(0f, 1f);
		content.OffsetMin.Value = new float2(size);
	}

	public void VerticalFooter(float size, out RectTransform footer, out RectTransform content)
	{
		content = Empty("Content").GetComponent<RectTransform>();
		footer = Empty("Footer").GetComponent<RectTransform>();
		footer.OffsetMin.Value = new float2(0f - size);
		footer.AnchorMin.Value = new float2(1f);
		footer.AnchorMax.Value = new float2(1f, 1f);
		content.OffsetMax.Value = new float2(0f - size);
	}

	public Button Button()
	{
		return Button((LocaleString)"");
	}

	public Button Button(in LocaleString text)
	{
		return Button(in text, new colorX?(Style.ButtonColor));
	}

	public Button Button(in LocaleString text, ButtonEventHandler action)
	{
		Button button = Button(in text);
		button.Pressed.Target = action;
		return button;
	}

	public Button Button(Uri icon, ButtonEventHandler action)
	{
		Button button = Button(icon);
		button.Pressed.Target = action;
		return button;
	}

	public Button Button(IAssetProvider<Sprite> sprite, LocaleString text)
	{
		return Button(in text, sprite, null, new colorX?(Style.ButtonColor), in Style.ButtonSpriteColor);
	}

	public Button Button(IAssetProvider<Sprite> sprite, in colorX spriteTint, LocaleString text, float buttonTextSplit = 0.33333f, float buttonTextSplitGap = 0.05f)
	{
		return Button(in text, sprite, null, new colorX?(Style.ButtonColor), in spriteTint, buttonTextSplit, buttonTextSplitGap);
	}

	public Button Button(Uri icon, LocaleString text)
	{
		return Button(in text, null, icon, new colorX?(Style.ButtonColor), in Style.ButtonSpriteColor);
	}

	public Button Button(Uri icon, LocaleString text, in colorX? tint, in colorX spriteTint)
	{
		return Button(in text, null, icon, in tint, in spriteTint);
	}

	public Button Button(Uri icon, LocaleString text, ButtonEventHandler action)
	{
		Button button = Button(icon, text);
		button.Pressed.Target = action;
		return button;
	}

	public Button Button<T>(Uri icon, LocaleString text, ButtonEventHandler<T> action, T argument)
	{
		Button button = Button(icon, text);
		button.SetupAction(action, argument);
		return button;
	}

	public Button Button(Uri icon, LocaleString text, in colorX? tint, in colorX spriteTint, ButtonEventHandler action)
	{
		Button button = Button(icon, text, in tint, in spriteTint);
		button.Pressed.Target = action;
		return button;
	}

	public Button Button(in LocaleString text, in colorX? tint, ButtonEventHandler action, float doublePressDelay = 0f)
	{
		return Button(in text, in tint).SetupAction(action, doublePressDelay);
	}

	public Button Button(Uri spriteUrl)
	{
		return Button(spriteUrl, new colorX?(Style.ButtonColor));
	}

	public Button Button(Uri spriteUrl, in colorX? buttonTint)
	{
		return Button(spriteUrl, in buttonTint, in Style.ButtonSpriteColor);
	}

	public Button Button(Uri spriteUrl, in colorX? buttonTint, in colorX spriteTint)
	{
		return Button((LocaleString)null, null, spriteUrl, in buttonTint, in spriteTint);
	}

	public Button Button(IAssetProvider<Sprite> sprite, in colorX? buttonTint, in colorX spriteTint)
	{
		return Button((LocaleString)null, sprite, null, in buttonTint, in spriteTint);
	}

	public Button Button(Uri spriteUrl, in colorX? buttonTint, in colorX spriteTint, ButtonEventHandler action, float doublePressDelay = 0f)
	{
		return Button((LocaleString)null, null, spriteUrl, in buttonTint, in spriteTint).SetupAction(action, doublePressDelay);
	}

	public Button Button<T>(Uri spriteUrl, ButtonEventHandler<T> action, T argument, float doublePressDelay = 0f)
	{
		Button button = Button((LocaleString)null, null, spriteUrl, (colorX?)null, colorX.White);
		button.SetupAction(action, argument, doublePressDelay);
		return button;
	}

	public Button Button<T>(Uri spriteUrl, in colorX? buttonTint, in colorX spriteTint, ButtonEventHandler<T> action, T argument, float doublePressDelay = 0f)
	{
		Button button = Button((LocaleString)null, null, spriteUrl, in buttonTint, in spriteTint);
		button.SetupAction(action, argument, doublePressDelay);
		return button;
	}

	public Button Button(in LocaleString text, in colorX? tint)
	{
		return Button(in text, null, null, in tint, colorX.White);
	}

	private Button Button(in LocaleString text, IAssetProvider<Sprite> sprite, Uri spriteUrl, in colorX? tint, in colorX spriteTint, float buttonTextSplit = 0.3333333f, float buttonTextSplitGap = 0.05f)
	{
		Next("Button");
		Image image = Current.AttachComponent<Image>();
		image.Tint.Value = tint ?? Style.ButtonColor;
		if (Style.ButtonZWrite)
		{
			image.Material.Target = World.GetDefaultUI_ZWrite();
		}
		if (Style.ButtonSprite != null)
		{
			image.Sprite.Target = Style.ButtonSprite;
			image.NineSliceSizing.Value = Style.NineSliceSizing;
		}
		Button button = Current.AttachComponent<Button>();
		button.PassThroughHorizontalMovement.Value = Style.PassThroughHorizontalMovement;
		button.PassThroughVerticalMovement.Value = Style.PassThroughVerticalMovement;
		button.RequireLockInToPress.Value = Style.RequireLockInToPress;
		if (Style.DisabledColor.HasValue)
		{
			button.ColorDrivers[0].DisabledColor.Value = Style.DisabledColor.Value;
		}
		bool num = spriteUrl != null || sprite != null;
		RectTransform left = null;
		RectTransform right = null;
		Nest();
		if (num && text != (LocaleString)null)
		{
			SplitHorizontally(buttonTextSplit, out left, out right, buttonTextSplitGap);
		}
		if (num)
		{
			if (left != null)
			{
				ForceNext = left;
			}
			Image image2 = Image(null, in spriteTint);
			if (spriteUrl != null)
			{
				image2.Sprite.Target = Current.AttachSprite(spriteUrl);
			}
			else
			{
				image2.Sprite.Target = sprite;
			}
			image2.RectTransform.AddFixedPadding(Style.ButtonIconPadding);
			if (Style.DisabledAlpha.HasValue)
			{
				button.SetupTransparentOnDisabled(image2.Tint, Style.DisabledAlpha.Value);
			}
		}
		if (text != (LocaleString)null)
		{
			if (right != null)
			{
				ForceNext = right;
			}
			Text text2 = Text(in text, bestFit: true, Style.ButtonTextAlignment);
			text2.RectTransform.AddFixedPadding(Style.ButtonTextPadding);
			if (Style.DisabledAlpha.HasValue)
			{
				button.SetupTransparentOnDisabled(text2.Color, Style.DisabledAlpha.Value);
			}
		}
		NestOut();
		return button;
	}

	public Button Button<T>(in LocaleString text, ButtonEventHandler<T> callback, T argument, float doublePressDelay = 0f)
	{
		Button button = Button(in text, new colorX?(Style.ButtonColor));
		button.SetupAction(callback, argument, doublePressDelay);
		return button;
	}

	public Button Button<T>(in LocaleString text, in colorX? tint, ButtonEventHandler<T> callback, T argument, float doublePressDelay = 0f)
	{
		Button button = Button(in text, in tint);
		button.SetupAction(callback, argument, doublePressDelay);
		return button;
	}

	public Button ButtonRef<T>(in LocaleString text, ButtonEventHandler<T> callback, T argument, float doublePressDelay = 0f) where T : class, IWorldElement
	{
		return ButtonRef(in text, (colorX?)null, callback, argument, doublePressDelay);
	}

	public Button ButtonRef<T>(in LocaleString text, in colorX? tint, ButtonEventHandler<T> callback, T argument, float doublePressDelay = 0f) where T : class, IWorldElement
	{
		Button button = Button(in text, in tint);
		ButtonRefRelay<T> buttonRefRelay = button.Slot.AttachComponent<ButtonRefRelay<T>>();
		buttonRefRelay.Argument.Target = argument;
		buttonRefRelay.ButtonPressed.Target = callback;
		buttonRefRelay.DoublePressDelay.Value = doublePressDelay;
		return button;
	}

	public ValueRadio<T> ValueRadio<T>(IField<T> valueField, T value)
	{
		ValueRadio<T> valueRadio = Radio<ValueRadio<T>>();
		valueRadio.OptionValue.Value = value;
		valueRadio.TargetValue.Target = valueField;
		return valueRadio;
	}

	public ReferenceRadio<T> ReferenceRadio<T>(SyncRef<T> refField, T target) where T : class, IWorldElement
	{
		ReferenceRadio<T> referenceRadio = Radio<ReferenceRadio<T>>();
		referenceRadio.OptionReference.Target = target;
		referenceRadio.TargetReference.Target = refField;
		return referenceRadio;
	}

	public ValueRadio<T> ValueRadio<T>(in LocaleString label, IField<T> valueField, T value)
	{
		Text text;
		return ValueRadio(in label, valueField, value, out text);
	}

	public ValueRadio<T> ValueRadio<T>(in LocaleString label, IField<T> valueField, T value, out Text text)
	{
		float size = MathX.Max(Style.MinHeight, Style.PreferredHeight);
		Panel();
		VerticalFooter(size, out RectTransform footer, out RectTransform content);
		NestInto(content);
		text = Text(in label, bestFit: true, Alignment.MiddleLeft);
		NestOut();
		NestInto(footer);
		ValueRadio<T> result = ValueRadio(valueField, value);
		NestOut();
		NestOut();
		return result;
	}

	public ReferenceRadio<T> ReferenceRadio<T>(string label, SyncRef<T> refField, T target) where T : class, IWorldElement
	{
		float size = MathX.Max(Style.MinHeight, Style.PreferredHeight);
		Panel();
		VerticalFooter(size, out RectTransform footer, out RectTransform content);
		NestInto(content);
		Text((LocaleString)label, bestFit: true, Alignment.MiddleLeft);
		NestOut();
		NestInto(footer);
		ReferenceRadio<T> result = ReferenceRadio(refField, target);
		NestOut();
		NestOut();
		return result;
	}

	public R Radio<R>() where R : Radio, new()
	{
		PushStyle();
		Style.MinWidth = Style.MinHeight;
		Style.PreferredWidth = Style.PreferredWidth;
		Panel();
		PopStyle();
		float num = MathX.Max(Style.MinHeight, Style.PreferredHeight);
		Image(CircleSprite, in Style.ButtonColor).RectTransform.Pivot.Value = new float2(0f, 0.5f);
		Current.AttachComponent<AspectRatioFitter>();
		Button button = Current.AttachComponent<Button>();
		R val = Current.AttachComponent<R>();
		button.RequireLockInToPress.Value = Style.RequireLockInToPress;
		Nest();
		Image image = Image(CircleSprite, in Style.TextColor);
		SetPadding(num * 0.1f);
		NestOut();
		NestOut();
		val.CheckVisual.Target = image.Slot.ActiveSelf_Field;
		return val;
	}

	public Checkbox Checkbox(in LocaleString label, bool state = false, bool labelFirst = true, float padding = 4f)
	{
		Text labelText;
		return Checkbox(in label, out labelText, state, labelFirst, padding);
	}

	public Checkbox Checkbox(in LocaleString label, out Text labelText, bool state = false, bool labelFirst = true, float padding = 4f)
	{
		float size = MathX.Max(Style.MinHeight, Style.PreferredHeight);
		Panel();
		RectTransform header;
		RectTransform content;
		if (labelFirst)
		{
			VerticalFooter(size, out header, out content);
			content.AddFixedPadding(0f, padding, 0f, 0f);
		}
		else
		{
			VerticalHeader(size, out header, out content);
			content.AddFixedPadding(0f, 0f, 0f, padding);
		}
		NestInto(content);
		labelText = Text(in label, bestFit: true, Alignment.MiddleLeft);
		NestOut();
		NestInto(header);
		Checkbox checkbox = Checkbox();
		checkbox.State.Value = state;
		NestOut();
		if (!IsAtRoot)
		{
			NestOut();
		}
		return checkbox;
	}

	public Checkbox Checkbox(bool state = false)
	{
		PushStyle();
		Style.MinWidth = Style.MinHeight;
		Panel();
		PopStyle();
		Image image = Image(in Style.ButtonColor);
		image.RectTransform.Pivot.Value = new float2(0f, 0.5f);
		image.Sprite.Target = Style.ButtonSprite;
		image.NineSliceSizing.Value = Style.NineSliceSizing;
		Current.AttachComponent<AspectRatioFitter>();
		Button button = Current.AttachComponent<Button>();
		Checkbox checkbox = Current.AttachComponent<Checkbox>();
		button.RequireLockInToPress.Value = Style.RequireLockInToPress;
		Nest();
		Image image2 = Image(CheckSprite, in Style.TextColor);
		image2.RectTransform.AddFixedPadding(Style.ButtonTextPadding);
		NestOut();
		NestOut();
		checkbox.CheckVisual.Target = image2.Slot.ActiveSelf_Field;
		checkbox.State.Value = state;
		return checkbox;
	}

	public RectMesh<M> RectMesh<M>(IAssetProvider<Material> material = null) where M : RectMeshSource, new()
	{
		Next("RectMesh");
		RectMesh<M> rectMesh = Current.AttachComponent<RectMesh<M>>();
		if (material != null)
		{
			rectMesh.Materials.Add(material);
		}
		return rectMesh;
	}

	public RawGraphic RawGraphic(IAssetProvider<Material> material = null, IAssetProvider<MaterialPropertyBlock> propertyBlock = null)
	{
		Next("RawGraphic");
		RawGraphic rawGraphic = Current.AttachComponent<RawGraphic>();
		rawGraphic.Material.Target = material;
		rawGraphic.PropertyBlock.Target = propertyBlock;
		return rawGraphic;
	}

	public RawImage RawImage(IAssetProvider<ITexture2D> texture, bool preserveAspect = false)
	{
		return RawImage(texture, colorX.White, preserveAspect);
	}

	public RawImage RawImage(IAssetProvider<ITexture2D> texture, in colorX tint, bool preserveAspect)
	{
		Next("RawImage");
		RawImage rawImage = Current.AttachComponent<RawImage>();
		rawImage.Texture.Target = texture;
		rawImage.PreserveAspect.Value = preserveAspect;
		return rawImage;
	}

	public TiledRawImage TiledRawImage(IAssetProvider<ITexture2D> texture, in colorX tint)
	{
		Next("TiledRawImage");
		TiledRawImage tiledRawImage = Current.AttachComponent<TiledRawImage>();
		tiledRawImage.Texture.Target = texture;
		tiledRawImage.Tint.Value = tint;
		return tiledRawImage;
	}

	public GradientImage Gradient(in colorX topLeft, in colorX topRight, in colorX bottomRight, in colorX bottomLeft)
	{
		Next("Gradient");
		GradientImage gradientImage = Current.AttachComponent<GradientImage>();
		gradientImage.TintTopLeft.Value = topLeft;
		gradientImage.TintTopRight.Value = topRight;
		gradientImage.TintBottomRight.Value = bottomRight;
		gradientImage.TintBottomLeft.Value = bottomLeft;
		return gradientImage;
	}

	public GradientImage VerticalGradient(in colorX top, in colorX bottom)
	{
		return Gradient(in top, in top, in bottom, in bottom);
	}

	public GradientImage HorizontalGradient(in colorX left, in colorX right)
	{
		return Gradient(in left, in right, in right, in left);
	}

	public Image Image()
	{
		return Image(colorX.White);
	}

	public Image Image(in colorX color, bool zwrite = false)
	{
		Image image = Image(null, in color);
		if (zwrite)
		{
			image.Material.Target = World.GetDefaultUI_ZWrite();
		}
		return image;
	}

	public Image Image(Uri url, int? maxSize = null)
	{
		return Image(url, colorX.White, maxSize);
	}

	public Image Image(Uri url, colorX tint, int? maxSize = null)
	{
		Image image = Image(in tint);
		image.Sprite.Target = Current.AttachSprite(url, uncompressed: false, evenNull: false, getExisting: true, maxSize);
		return image;
	}

	public Image Image(IAssetProvider<ITexture2D> tex)
	{
		SpriteProvider spriteProvider = Root.AttachComponent<SpriteProvider>();
		spriteProvider.Texture.Target = tex;
		return Image(spriteProvider);
	}

	public Image Image(IAssetProvider<Sprite> sprite, bool preserveAspect = true)
	{
		return Image(sprite, colorX.White, preserveAspect);
	}

	public Image Image(IAssetProvider<Sprite> sprite, in colorX tint, bool preserveAspect = true)
	{
		Next("Image");
		Image image = Current.AttachComponent<Image>();
		image.Tint.Value = tint;
		image.Sprite.Target = sprite;
		image.PreserveAspect.Value = preserveAspect;
		return image;
	}

	public Mask Mask()
	{
		return Mask(colorX.White);
	}

	public Mask Mask(in colorX color, bool showMaskGraphic = false, bool zwrite = false)
	{
		Image(in color, zwrite);
		Mask mask = Current.AttachComponent<Mask>();
		mask.ShowMaskGraphic.Value = showMaskGraphic;
		return mask;
	}

	public Mask SpriteMask(IAssetProvider<Sprite> sprite, bool showMaskGraphic = false)
	{
		Image image;
		return SpriteMask(sprite, showMaskGraphic, out image);
	}

	public Mask SpriteMask(IAssetProvider<Sprite> sprite, bool showMaskGraphic, out Image image)
	{
		image = Image(sprite);
		Mask mask = Current.AttachComponent<Mask>();
		mask.ShowMaskGraphic.Value = showMaskGraphic;
		return mask;
	}

	public TextField PasswordField()
	{
		TextField textField = TextField("", undo: false, null, parseRTF: false);
		textField.Text.MaskPattern.Value = "*";
		return textField;
	}

	public TextField TextField(string defaultText = "", bool undo = false, string undoDescription = null, bool parseRTF = true, LocaleString promptText = default(LocaleString))
	{
		Button button = Button();
		if (Style.TextFieldSprite != null)
		{
			button.Slot.GetComponent<Image>().Sprite.Target = Style.TextFieldSprite;
		}
		TextField textField = Current.AttachComponent<TextField>();
		Text componentInChildren = Current.GetComponentInChildren<Text>();
		componentInChildren.ParseRichText.Value = parseRTF;
		if (promptText != default(LocaleString))
		{
			componentInChildren.NullContent.SetLocalized(promptText);
		}
		if (Style.Font != null)
		{
			componentInChildren.Font.Target = Style.Font;
		}
		textField.Text = componentInChildren;
		textField.Editor.Target.Undo.Value = undo;
		textField.Editor.Target.UndoDescription.Value = undoDescription;
		textField.TargetString = defaultText;
		return textField;
	}

	public T HorizontalElementWithLabel<T>(in LocaleString label, float separation, Func<T> elementBuilder, float gap = 0.01f) where T : Component
	{
		Text labelText;
		return HorizontalElementWithLabel(in label, separation, elementBuilder, out labelText, gap);
	}

	public T HorizontalElementWithLabel<T>(in LocaleString label, float separation, Func<T> elementBuilder, out Text labelText, float gap = 0.01f) where T : Component
	{
		Panel();
		List<RectTransform> list = SplitHorizontally(separation, gap, 1f - (separation + gap));
		NestInto(list[0]);
		labelText = Text(in label, bestFit: true, Alignment.MiddleLeft);
		NestOut();
		NestInto(list[2]);
		T result = elementBuilder();
		NestOut();
		NestOut();
		return result;
	}

	public IntTextEditorParser IntegerField(int min = int.MinValue, int max = int.MaxValue, int increments = 1, bool parseContinuously = true)
	{
		IntTextEditorParser intTextEditorParser = TextField().Editor.Target.Slot.AttachComponent<IntTextEditorParser>();
		intTextEditorParser.ParseContinuously.Value = parseContinuously;
		intTextEditorParser.Min.Value = min;
		intTextEditorParser.Max.Value = max;
		intTextEditorParser.Increments.Value = increments;
		return intTextEditorParser;
	}

	public FloatTextEditorParser FloatField(float min = float.MinValue, float max = float.MaxValue, int decimalPlaces = 2, string format = null, bool parseContinuously = true)
	{
		FloatTextEditorParser floatTextEditorParser = TextField().Editor.Target.Slot.AttachComponent<FloatTextEditorParser>();
		floatTextEditorParser.ParseContinuously.Value = parseContinuously;
		floatTextEditorParser.Min.Value = min;
		floatTextEditorParser.Max.Value = max;
		floatTextEditorParser.DecimalPlaces.Value = decimalPlaces;
		floatTextEditorParser.StringFormat.Value = format;
		return floatTextEditorParser;
	}

	public QuantityTextEditorParser<U, T> QuantityField<U, T>(bool parseContinuously = true) where U : unmanaged, IQuantity<U> where T : IConvertible
	{
		QuantityTextEditorParser<U, T> quantityTextEditorParser = TextField().Editor.Target.Slot.AttachComponent<QuantityTextEditorParser<U, T>>();
		quantityTextEditorParser.ParseContinuously.Value = parseContinuously;
		return quantityTextEditorParser;
	}

	public QuantityTextEditorParser<U, T> QuantityField<U, T>(U min, U max, bool parseContinuously = true) where U : unmanaged, IQuantity<U> where T : IConvertible
	{
		QuantityTextEditorParser<U, T> quantityTextEditorParser = TextField().Editor.Target.Slot.AttachComponent<QuantityTextEditorParser<U, T>>();
		quantityTextEditorParser.MinValue.Value = (T)Convert.ChangeType(min.BaseValue, typeof(T));
		quantityTextEditorParser.MaxValue.Value = (T)Convert.ChangeType(max.BaseValue, typeof(T));
		quantityTextEditorParser.ParseContinuously.Value = parseContinuously;
		return quantityTextEditorParser;
	}

	public Slider<float> Slider(float height, float value = 0f, float min = 0f, float max = 1f, bool integers = false)
	{
		return this.Slider<float>(height, value, min, max, integers);
	}

	public Slider<float> Slider(float height, out Image line, out Image fillLine, out Image handle)
	{
		return Slider(height, 0f, 0f, 1f, integers: false, out line, out fillLine, out handle);
	}

	public Slider<T> Slider<T>(float height, T? value = null, T? min = null, T? max = null, bool integers = false) where T : struct
	{
		Image line;
		Image fillLine;
		Image handle;
		return Slider(height, value.GetValueOrDefault(), min.GetValueOrDefault(), max ?? Coder<T>.Identity, integers, out line, out fillLine, out handle);
	}

	public Slider<T> Slider<T>(float height, T value, T min, T max, bool integers = false)
	{
		T val = value;
		T value2 = ((val != null) ? val : default(T));
		val = min;
		T min2 = ((val != null) ? val : default(T));
		val = max;
		Image line;
		Image fillLine;
		Image handle;
		return Slider(height, value2, min2, (val != null) ? val : Coder<T>.Identity, integers, out line, out fillLine, out handle);
	}

	public Slider<T> Slider<T>(float height, out Image line, out Image fillLine, out Image handle)
	{
		return Slider(height, default(T), default(T), Coder<T>.Identity, integers: false, out line, out fillLine, out handle);
	}

	public Slider<T> Slider<T>(float height, T value, T min, T max, bool integers, out Image line, out Image fillLine, out Image handle)
	{
		Next("Slider");
		Slider<T> slider = Current.AttachComponent<Slider<T>>();
		slider.RequireLockInToInteract.Value = true;
		Nest();
		Next("Background");
		Current.AttachComponent<Image>().Tint.Value = colorX.Clear;
		float num = height * 0.5f;
		RectTransform component = Current.GetComponent<RectTransform>();
		component.OffsetMin.Value = new float2(num);
		component.OffsetMax.Value = new float2(0f - num);
		Nest();
		line = Image(in Style.ButtonColor);
		line.Sprite.Target = Style.ButtonSprite;
		line.NineSliceSizing.Value = Style.NineSliceSizing;
		line.RectTransform.SetFixedVertical((0f - height) / 4f, height / 4f, 0.5f);
		fillLine = Image(in Style.SliderFillColor);
		fillLine.Sprite.Target = Style.ButtonSprite;
		fillLine.NineSliceSizing.Value = Style.NineSliceSizing;
		fillLine.RectTransform.SetFixedVertical((0f - height) / 4f, height / 4f, 0.5f);
		NestOut();
		Next("HandleArea");
		RectTransform component2 = Current.GetComponent<RectTransform>();
		component2.OffsetMin.Value = new float2(num);
		component2.OffsetMax.Value = new float2(0f - num);
		Nest();
		handle = Image(CircleSprite, in Style.TextColor);
		handle.InteractionTarget.Value = false;
		handle.RectTransform.SetFixedRect(new Rect(float2.One * (0f - num), float2.One * height));
		handle.Slot.Name = "Handle";
		slider.HandleAnchorMinDrive.Target = handle.RectTransform.AnchorMin;
		slider.HandleAnchorMaxDrive.Target = handle.RectTransform.AnchorMax;
		slider.FillLineDrive.Target = fillLine.RectTransform.AnchorMax;
		slider.Min.Value = min;
		slider.Max.Value = max;
		slider.Value.Value = value;
		slider.Integers.Value = integers;
		slider.ColorDrivers.Add().ColorDrive.Target = handle.Tint;
		NestOut();
		NestOut();
		return slider;
	}

	/// <summary>
	/// Creates a horizontal layout for UI elements.  Will automatically nest subsequent UI elements until a matching NestOut call is made.
	/// </summary>
	/// <param name="spacing"></param>
	/// <param name="padding"></param>
	/// <param name="childAlignment"></param>
	/// <returns></returns>
	public HorizontalLayout HorizontalLayout(float spacing = 0f, float padding = 0f, Alignment? childAlignment = null)
	{
		return HorizontalLayout(spacing, padding, padding, padding, padding, childAlignment);
	}

	public HorizontalLayout HorizontalLayout(float spacing, float paddingTop, float paddingRight, float paddingBottom, float paddingLeft, Alignment? childAlignment = null)
	{
		NextForLayout("Horizontal Layout");
		HorizontalLayout horizontalLayout = Current.AttachComponent<HorizontalLayout>();
		horizontalLayout.Spacing.Value = spacing;
		horizontalLayout.PaddingTop.Value = paddingTop;
		horizontalLayout.PaddingRight.Value = paddingRight;
		horizontalLayout.PaddingBottom.Value = paddingBottom;
		horizontalLayout.PaddingLeft.Value = paddingLeft;
		horizontalLayout.ChildAlignment = childAlignment ?? Style.ChildAlignment;
		horizontalLayout.ForceExpandHeight.Value = Style.ForceExpandHeight;
		horizontalLayout.ForceExpandWidth.Value = Style.ForceExpandWidth;
		Nest();
		return horizontalLayout;
	}

	/// <summary>
	/// Creates a vertical layout for UI elements.  Will automatically nest subsequent UI elements until a matching NestOut call is made.
	/// </summary>
	/// <param name="spacing"></param>
	/// <param name="padding"></param>
	/// <param name="childAlignment"></param>
	/// <returns></returns>
	public VerticalLayout VerticalLayout(float spacing = 0f, float padding = 0f, Alignment? childAlignment = null, bool? forceExpandWidth = null, bool? forceExpandHeight = null)
	{
		return VerticalLayout(spacing, padding, padding, padding, padding, childAlignment, forceExpandWidth, forceExpandHeight);
	}

	public VerticalLayout VerticalLayout(float spacing, float paddingTop, float paddingRight, float paddingBottom, float paddingLeft, Alignment? childAlignment = null, bool? forceExpandWidth = null, bool? forceExpandHeight = null)
	{
		NextForLayout("Vertical Layout");
		VerticalLayout verticalLayout = Current.AttachComponent<VerticalLayout>();
		verticalLayout.Spacing.Value = spacing;
		verticalLayout.PaddingTop.Value = paddingTop;
		verticalLayout.PaddingRight.Value = paddingRight;
		verticalLayout.PaddingBottom.Value = paddingBottom;
		verticalLayout.PaddingLeft.Value = paddingLeft;
		verticalLayout.ChildAlignment = childAlignment ?? Style.ChildAlignment;
		verticalLayout.ForceExpandHeight.Value = forceExpandHeight ?? Style.ForceExpandHeight;
		verticalLayout.ForceExpandWidth.Value = forceExpandWidth ?? Style.ForceExpandWidth;
		Nest();
		return verticalLayout;
	}

	public OverlappingLayout OverlappingLayout(float padding = 0f, Alignment? childAlignment = null)
	{
		return OverlappingLayout(padding, padding, padding, padding, childAlignment);
	}

	public OverlappingLayout OverlappingLayout(float paddingTop, float paddingRight, float paddingBottom, float paddingLeft, Alignment? childAlignment = null)
	{
		NextForLayout("Overlapping Layout");
		OverlappingLayout overlappingLayout = Current.AttachComponent<OverlappingLayout>();
		overlappingLayout.PaddingTop.Value = paddingTop;
		overlappingLayout.PaddingRight.Value = paddingRight;
		overlappingLayout.PaddingBottom.Value = paddingBottom;
		overlappingLayout.PaddingLeft.Value = paddingLeft;
		overlappingLayout.ChildAlignment = childAlignment ?? Style.ChildAlignment;
		overlappingLayout.ForceExpandHeight.Value = Style.ForceExpandHeight;
		overlappingLayout.ForceExpandWidth.Value = Style.ForceExpandWidth;
		Nest();
		return overlappingLayout;
	}

	/// <summary>
	/// Creates a grid layout for UI elements.  Will automatically nest subsequent UI elements until a matching NestOut call is made.
	/// </summary>
	/// <returns></returns>
	public GridLayout GridLayout()
	{
		return GridLayout(float2.One * 64, float2.Zero);
	}

	public GridLayout GridLayout(in float2 cellSize)
	{
		return GridLayout(in cellSize, float2.Zero);
	}

	public GridLayout GridLayout(in float2 cellSize, in float2 spacing, Alignment childAlignment = Alignment.MiddleCenter)
	{
		NextForLayout("Grid Layout");
		GridLayout gridLayout = Current.AttachComponent<GridLayout>();
		gridLayout.Spacing.Value = spacing;
		gridLayout.CellSize.Value = cellSize;
		gridLayout.ChildAlignment = childAlignment;
		Nest();
		return gridLayout;
	}

	/// <summary>
	/// Creates a scroll area for UI elements.  Will automatically nest subsequent UI elements until a matching NestOut call is made.
	/// </summary>
	/// <param name="alignment"></param>
	/// <returns></returns>
	public ScrollRect ScrollArea(Alignment? alignment = null)
	{
		Mask mask;
		Image graphic;
		return ScrollArea<Image>(alignment, out mask, out graphic);
	}

	public ScrollRect ScrollArea<G>(Alignment? alignment, out Mask mask, out G graphic) where G : Graphic, new()
	{
		Next("Scroll Area");
		Slot content;
		ScrollRect scrollRect = ScrollRect.CreateScrollRect<G>(Current, out content, out mask, out graphic);
		if (alignment.HasValue)
		{
			scrollRect.Alignment = alignment.Value;
		}
		Current = content;
		Nest();
		LayoutTarget = content;
		return scrollRect;
	}

	public ContentSizeFitter FitContent()
	{
		return FitContent(SizeFit.PreferredSize);
	}

	public ContentSizeFitter FitContent(SizeFit fit)
	{
		return FitContent(fit, fit);
	}

	public ContentSizeFitter FitContent(SizeFit horizontal, SizeFit vertical)
	{
		Slot slot = Current ?? Root;
		ContentSizeFitter obj = slot.GetComponent<ContentSizeFitter>() ?? slot.AttachComponent<ContentSizeFitter>();
		obj.HorizontalFit.Value = horizontal;
		obj.VerticalFit.Value = vertical;
		return obj;
	}

	public IgnoreLayout IgnoreLayout()
	{
		return (Current ?? Root).GetComponentOrAttach<IgnoreLayout>();
	}

	public void SetFixedSize(in float2 anchor, in float2 size)
	{
		CurrentRect.AnchorMin.Value = anchor;
		CurrentRect.AnchorMax.Value = anchor;
		CurrentRect.OffsetMin.Value = float2.Zero;
		CurrentRect.OffsetMax.Value = size;
	}

	public void SetFixedHeight(float height, float heightAnchor = 0f)
	{
		CurrentRect.AnchorMin.Value = new float2(0f, heightAnchor);
		CurrentRect.AnchorMax.Value = new float2(1f, heightAnchor);
		CurrentRect.OffsetMin.Value = float2.Zero;
		CurrentRect.OffsetMax.Value = new float2(0f, height);
	}

	public void SetFixedWeight(float width, float widthAnchor = 0f)
	{
		CurrentRect.AnchorMin.Value = new float2(widthAnchor);
		CurrentRect.AnchorMax.Value = new float2(widthAnchor, 1f);
		CurrentRect.OffsetMin.Value = float2.Zero;
		CurrentRect.OffsetMax.Value = new float2(width);
	}

	public void SetPadding(float padding)
	{
		SetPadding(padding, padding, padding, padding);
	}

	public void SetPadding(float top, float right, float bottom, float left)
	{
		CurrentRect.OffsetMin.Value = new float2(left, top);
		CurrentRect.OffsetMax.Value = new float2(0f - right, 0f - bottom);
	}

	public static void SetupButtonColor(Button button, OutlinedArc arc)
	{
		InteractionElement.ColorDriver colorDriver = button.ColorDrivers.Add();
		InteractionElement.ColorDriver colorDriver2 = button.ColorDrivers.Add();
		colorDriver.ColorDrive.Target = arc.FillColor;
		colorDriver2.ColorDrive.Target = arc.OutlineColor;
		colorDriver.SetColors(new colorX(0.9f));
		colorDriver2.SetColors(new colorX(0.1f));
	}

	public ArcData Arc(in LocaleString label = default(LocaleString), bool setupButton = true)
	{
		Next("Arc");
		ArcData result = new ArcData
		{
			arc = Current.AttachComponent<OutlinedArc>(),
			arcLayout = Current.AttachComponent<ArcSegmentLayout>()
		};
		if (setupButton)
		{
			result.button = Current.AttachComponent<Button>();
			SetupButtonColor(result.button, result.arc);
		}
		Nest();
		result.image = Image();
		result.arcLayout.Nested.Target = result.image.RectTransform;
		if (label != (LocaleString)null)
		{
			result.text = Text(in label);
			result.arcLayout.Label.Target = result.text;
		}
		NestOut();
		return result;
	}
}
