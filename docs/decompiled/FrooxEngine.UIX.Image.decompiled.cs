using System;
using Elements.Core;
using Elements.Data;

namespace FrooxEngine.UIX;

[OldTypeName("FrooxEngine.UI.Image", null)]
public class Image : ImageBase
{
	public readonly Sync<colorX> Tint;

	private colorX _tint;

	public override int Version => 1;

	protected override colorX ColorTopLeft => _tint;

	protected override colorX ColorTopRight => _tint;

	protected override colorX ColorBottomRight => _tint;

	protected override colorX ColorBottomLeft => _tint;

	protected override void OnAwake()
	{
		base.OnAwake();
		Tint.Value = colorX.White;
	}

	public override void PrepareCompute()
	{
		base.PrepareCompute();
		_tint = Tint.Value;
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Tint = new Sync<colorX>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Sprite, 
			4 => Material, 
			5 => PreserveAspect, 
			6 => NineSliceSizing, 
			7 => FlipHorizontally, 
			8 => FlipVertically, 
			9 => InteractionTarget, 
			10 => FillRect, 
			11 => __legacyZWrite, 
			12 => Tint, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static Image __New()
	{
		return new Image();
	}
}
