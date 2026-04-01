public class DataFeedResettableGroup : DataFeedGroup
{
	public Action<SyncDelegate<Action>>? ResetAction { get; private set; }

	public void InitResetAction(Action<SyncDelegate<Action>> setupResetAction)
	{
		ResetAction = setupResetAction;
	}
}
