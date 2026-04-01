using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Elements.Core;
using SkyFrost.Base;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: AssemblyTitle("FrooxEngine.Commands")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Yellow Dog Man Studios")]
[assembly: AssemblyCopyright("Copyright © 2023")]
[assembly: AssemblyProduct("FrooxEngine.Commands")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: Guid("aecfbd21-55b0-4bf0-8fc1-ee6113b0065a")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: TargetFramework(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
[assembly: AssemblyVersion("1.0.0.0")]
[module: RefSafetyRules(11)]
namespace FrooxEngine.Commands;

public static class CommandInterface
{
	public static bool IsCommandServerRunning
	{
		get
		{
			using TcpClient tcpClient = Connect(0);
			if (tcpClient != null)
			{
				NetworkStream stream = tcpClient.GetStream();
				stream.WriteByte(0);
				stream.Flush();
				tcpClient.Close();
			}
			return tcpClient != null;
		}
	}

	public static void SendCommand(BaseCommand obj, int port = 41245)
	{
		using TcpClient tcpClient = Connect(30000, port);
		if (tcpClient != null)
		{
			NetworkStream stream = tcpClient.GetStream();
			byte[] array = JsonSerializer.SerializeToUtf8Bytes(obj);
			stream.Write(array, 0, array.Length);
			stream.Flush();
			tcpClient.Close();
		}
	}

	private static TcpClient Connect(int timeout = 30000, int port = 41245)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		do
		{
			try
			{
				TcpClient tcpClient = new TcpClient("127.0.0.1", port);
				if (tcpClient.Connected)
				{
					return tcpClient;
				}
			}
			catch
			{
			}
		}
		while (stopwatch.ElapsedMilliseconds < timeout);
		return null;
	}
}
public static class CommandConstants
{
	public const int PROTOCOL_VERSION = 1;
}
public class CommandServer : IDisposable
{
	public const ushort PORT = 41245;

	private const int PORT_IN_USE_CODE = 10048;

	private Action<BaseCommand> callback;

	private TcpListener listener;

	private CancellationTokenSource cancellationTokenSource;

	public CommandServer(Action<BaseCommand> commandCallback, int port = 41245)
	{
		callback = commandCallback;
		try
		{
			TcpListener tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
			tcpListener.Start();
			listener = tcpListener;
		}
		catch (SocketException ex)
		{
			if (ex.ErrorCode == 10048)
			{
				UniLog.Log($"Unable to start FrooxEngine.CommandServer on port: {port}, is it occupied?");
			}
			else
			{
				UniLog.Log("EXCEPTION starting command listener: " + ex);
			}
		}
		catch (Exception ex2)
		{
			UniLog.Log("EXCEPTION starting command listener: " + ex2);
		}
		if (listener != null)
		{
			cancellationTokenSource = new CancellationTokenSource();
			Task.Run(async delegate
			{
				await CommandListener(cancellationTokenSource.Token);
			}, cancellationTokenSource.Token);
		}
	}

	public void Dispose()
	{
		cancellationTokenSource?.Cancel();
		cancellationTokenSource = null;
	}

	private async Task CommandListener(CancellationToken cancellationToken)
	{
		while (!cancellationTokenSource.IsCancellationRequested)
		{
			TcpClient client = null;
			try
			{
				client = await listener.AcceptTcpClientAsync().ConfigureAwait(continueOnCapturedContext: false);
				UniLog.Log("Accepted FrooxEngine.Command TcpListener");
				BaseCommand baseCommand = JsonSerializer.Deserialize<BaseCommand>(client.GetStream());
				if (baseCommand != null && baseCommand.Version == 1)
				{
					callback(baseCommand);
					continue;
				}
				if (baseCommand == null)
				{
					UniLog.Log("Discarding invalid FrooxEngine.Command");
					continue;
				}
				UniLog.Log($"Discarding FrooxEngine.Commands. Protocol Version Missmatch Ours: {1}, theirs: {1}");
			}
			catch (JsonException)
			{
				UniLog.Log("Ignoring Invalid JSON in a FrooxEngine.Command");
			}
			catch (Exception ex2)
			{
				UniLog.Log("Exception processing Command: " + ex2);
			}
			finally
			{
				client?.Dispose();
			}
		}
		listener.Stop();
	}
}
[JsonPolymorphic(TypeDiscriminatorPropertyName = "command")]
[JsonDerivedType(typeof(OpenURL), "openURL")]
[JsonDerivedType(typeof(OpenFile), "openFile")]
public class BaseCommand
{
	[JsonPropertyName("version")]
	public int Version { get; set; } = 1;
}
public class OpenFile : BaseCommand
{
	[JsonConverter(typeof(JsonUriConverter))]
	[JsonPropertyName("file")]
	public Uri File { get; set; }
}
public class OpenURL : BaseCommand
{
	[JsonConverter(typeof(JsonUriConverter))]
	[JsonPropertyName("url")]
	public Uri URL { get; set; }

	public OpenURL(Uri url)
	{
		URL = url;
	}
}
