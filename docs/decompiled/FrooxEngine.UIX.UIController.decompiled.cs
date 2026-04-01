using Elements.Core;

namespace FrooxEngine.UIX;

public abstract class UIController : UIComputeComponent
{
	public virtual bool InteractionTarget => false;

	public virtual bool ForceGraphicChunk => false;

	public virtual Rect OnPostComputeSelfRect()
	{
		return base.RectTransform.LocalComputeRect;
	}

	public virtual void OnPostComputeRectChildren()
	{
	}

	public virtual void OnComputingBounds(in float2 offset)
	{
	}

	public virtual void OnPreSubmitChanges()
	{
	}

	public virtual void OnMainSubmitChanges(int renderDataOffset, ref int renderOffset, ref int maskDepth, ref Rect maskRect)
	{
	}

	public virtual void OnRemoved(RectTransform fromRect)
	{
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
	}
}
