using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elements.Core;
using Renderite.Shared;

namespace FrooxEngine;

public class SecurityManager
{
	private readonly struct AccessKey : IEquatable<AccessKey>
	{
		public readonly string host;

		public readonly int port;

		public readonly HostAccessScope accessType;

		public AccessKey(string host, int port, HostAccessScope accessType)
		{
			this.host = host;
			this.port = port;
			this.accessType = accessType;
		}

		public bool Equals(AccessKey other)
		{
			if (host == other.host && port == other.port)
			{
				return accessType == other.accessType;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return ((-191288632 * -1521134295 + EqualityComparer<string>.Default.GetHashCode(host)) * -1521134295 + port.GetHashCode()) * -1521134295 + accessType.GetHashCode();
		}
	}

	private HostAccessSettings _settings;

	private Dictionary<AccessKey, TaskCompletionSource<HostAccessPermission>> _accessRequests = new Dictionary<AccessKey, TaskCompletionSource<HostAccessPermission>>();

	private HashSet<AccessKey> _temporarilyAllowed = new HashSet<AccessKey>();

	public static int MAX_ACTIVE_REQUESTS => 5;

	public Engine Engine { get; private set; }

	public SecurityManager(Engine engine)
	{
		Engine = engine;
		Settings.RegisterComponentChanges<HostAccessSettings>(OnSettingsChanged);
	}

	private void OnSettingsChanged(HostAccessSettings settings)
	{
		_settings = settings;
	}

	public void TemporarilyAllowHTTP(string host)
	{
		lock (_temporarilyAllowed)
		{
			_temporarilyAllowed.Add(new AccessKey(host, 0, HostAccessScope.HTTP));
		}
	}

	public void TemporarilyAllowWebsocket(string host, int port)
	{
		lock (_temporarilyAllowed)
		{
			_temporarilyAllowed.Add(new AccessKey(host, port, HostAccessScope.Websocket));
		}
	}

	public bool IsTemporarilyAllowed(string host, int port, HostAccessScope accessType)
	{
		lock (_temporarilyAllowed)
		{
			if (accessType == HostAccessScope.HTTP)
			{
				port = 0;
			}
			if (_temporarilyAllowed.Contains(new AccessKey(host, port, accessType)))
			{
				return true;
			}
			if (accessType == HostAccessScope.Everything && _temporarilyAllowed.Contains(new AccessKey(host, 0, HostAccessScope.HTTP)) && _temporarilyAllowed.Contains(new AccessKey(host, port, HostAccessScope.Websocket)))
			{
				return true;
			}
		}
		return false;
	}

	public void TemporarilyAllowOSC_Sender(string host, int port)
	{
		lock (_temporarilyAllowed)
		{
			_temporarilyAllowed.Add(new AccessKey(host, port, HostAccessScope.OSC_Sender));
		}
	}

	public void TemporarilyAllowOSC_Receiver(int port)
	{
		lock (_temporarilyAllowed)
		{
			_temporarilyAllowed.Add(new AccessKey("localhost", port, HostAccessScope.OSC_Sender));
		}
	}

	public bool? CanAccess(string host, int port, HostAccessScope accessType)
	{
		if (string.IsNullOrEmpty(host))
		{
			return false;
		}
		if (IsTemporarilyAllowed(host, port, accessType))
		{
			return true;
		}
		if (_settings == null)
		{
			return null;
		}
		switch (accessType)
		{
		case HostAccessScope.HTTP:
			return _settings.CanMakeHTTP_Requests(host, port);
		case HostAccessScope.Websocket:
			return _settings.CanConnectToWebsocket(host, port);
		case HostAccessScope.OSC_Sender:
			return _settings.CanSendOSC(host, port);
		case HostAccessScope.OSC_Receiver:
			if (host != "localhost")
			{
				return false;
			}
			return _settings.CanReceiveOSC(port);
		case HostAccessScope.Everything:
			return CombineCanAccess(_settings.CanMakeHTTP_Requests(host, port), _settings.CanConnectToWebsocket(host, port), _settings.CanReceiveOSC(port), _settings.CanSendOSC(host, port));
		default:
			throw new NotImplementedException("Unsupported host access type: " + accessType);
		}
	}

	private static bool? CombineCanAccess(params bool?[] flags)
	{
		for (int i = 0; i < flags.Length; i++)
		{
			if (!flags[i].HasValue)
			{
				return null;
			}
		}
		for (int j = 0; j < flags.Length; j++)
		{
			if (flags[j] == false)
			{
				return false;
			}
		}
		return true;
	}

	public async Task<HostAccessPermission> RequestAccessPermission(string host, int port, HostAccessScope accessType, string reason)
	{
		if (string.IsNullOrWhiteSpace(host))
		{
			return HostAccessPermission.Denied;
		}
		AccessKey accessKey = new AccessKey(host, port, accessType);
		if (IsTemporarilyAllowed(host, port, accessType))
		{
			return HostAccessPermission.Allowed;
		}
		if (_settings == null)
		{
			_settings = await Settings.GetActiveSettingAsync<HostAccessSettings>();
		}
		bool? flag = CanAccess(host, port, accessType);
		if (flag.HasValue)
		{
			return flag.Value ? HostAccessPermission.Allowed : HostAccessPermission.Denied;
		}
		if (Engine.InputInterface.HeadOutputDevice == HeadOutputDevice.Headless || Engine.InputInterface.HeadOutputDevice.IsCamera())
		{
			return HostAccessPermission.Denied;
		}
		bool flag2;
		TaskCompletionSource<HostAccessPermission> task;
		lock (_accessRequests)
		{
			flag2 = !_accessRequests.TryGetValue(accessKey, out task);
			if (flag2)
			{
				if (_accessRequests.Count >= MAX_ACTIVE_REQUESTS)
				{
					return HostAccessPermission.Denied;
				}
				task = new TaskCompletionSource<HostAccessPermission>();
				_accessRequests.Add(accessKey, task);
			}
		}
		if (!flag2)
		{
			return await task.Task;
		}
		await Userspace.UserspaceWorld.Coroutines.StartTask(async delegate
		{
			do
			{
				await default(NextUpdate);
			}
			while (HostAccessDialog.Current != null);
			bool? flag3 = CanAccess(host, port, accessType);
			if (flag3.HasValue)
			{
				task.TrySetResult(flag3.Value ? HostAccessPermission.Allowed : HostAccessPermission.Denied);
				return;
			}
			Slot slot = Userspace.UserspaceWorld.AddSlot("Host Access");
			slot.PositionInFrontOfUser(float3.Backward);
			slot.AttachComponent<HostAccessDialog>().Setup(host, port, accessType, reason, task);
			HostAccessPermission dialogResult = await task.Task;
			if (dialogResult != HostAccessPermission.Ignored)
			{
				Settings.UpdateActiveSetting(delegate(HostAccessSettings s)
				{
					switch (accessType)
					{
					case HostAccessScope.HTTP:
						if (dialogResult == HostAccessPermission.Allowed)
						{
							s.AllowHTTP_Requests(host, port, reason);
						}
						else
						{
							s.BlockHTTP_Requests(host, port, reason);
						}
						break;
					case HostAccessScope.Websocket:
						if (dialogResult == HostAccessPermission.Allowed)
						{
							s.AllowWebsocket(host, port, reason);
						}
						else
						{
							s.BlockWebsocket(host, port, reason);
						}
						break;
					case HostAccessScope.OSC_Sender:
						if (dialogResult == HostAccessPermission.Allowed)
						{
							s.AllowOSC_Sending(host, port, reason);
						}
						else
						{
							s.BlockOSC_Sending(host, port, reason);
						}
						break;
					case HostAccessScope.OSC_Receiver:
						if (dialogResult == HostAccessPermission.Allowed)
						{
							s.AllowOSC_Receiving(port, reason);
						}
						else
						{
							s.BlockOSC_Receiving(port, reason);
						}
						break;
					case HostAccessScope.Everything:
						if (dialogResult == HostAccessPermission.Allowed)
						{
							s.AllowHTTP_Requests(host, port, reason);
							s.AllowWebsocket(host, port, reason);
							s.AllowOSC_Receiving(port, reason);
							s.AllowOSC_Sending(host, port, reason);
						}
						else
						{
							s.BlockHTTP_Requests(host, port, reason);
							s.BlockWebsocket(host, port, reason);
							s.BlockOSC_Receiving(port, reason);
							s.BlockOSC_Sending(host, port, reason);
						}
						break;
					default:
						throw new NotImplementedException("Unsupported access type: " + accessType);
					}
				});
			}
			lock (_accessRequests)
			{
				_accessRequests.Remove(accessKey);
			}
		});
		return await task.Task;
	}
}
