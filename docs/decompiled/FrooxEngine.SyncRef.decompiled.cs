using System.Runtime.CompilerServices;

namespace FrooxEngine;

public sealed class SyncRef : SyncRef<IWorldElement>
{
	public override IWorldElement Target
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			return base.Target;
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set
		{
			base.Target = value;
		}
	}
}
