public class OWOTCPClient
{
	private NetworkStream stream;

	private TcpClient client;

	/// <summary>
	/// Delegate triggered on OWOConnectionHandler started
	/// </summary>
	public static Action OnOWOConnectionHandlerStarted;

	/// <summary>
	/// Delegate triggered on connection open
	/// </summary>
	public Action OnConnectionOpen;

	/// <summary>
	/// Delegate triggered on connection closed
	/// </summary>
	public Action OnConnectionClosed;

	/// <summary>
	/// Delegate triggered when game send a sensation to OWO app
	/// </summary>
	public Action<int, OWOMuscles> OnSensationSent;

	/// <summary>
	/// Delegate triggered on log messages
	/// </summary>
	public Action<string> OnLog;

	public string ip { get; set; } = "127.0.0.1";

	public int port { get; set; } = 54010;

	public OWOTCPClient()
	{
		OnOWOConnectionHandlerStarted = delegate
		{
			OnLog?.Invoke("OWOConnectionHandler <b>created</b>");
		};
		OnConnectionOpen = delegate
		{
			OnLog?.Invoke("OWO connection <b>open</b>");
		};
		OnConnectionClosed = delegate
		{
			OnLog?.Invoke("OWO connection <b>closed</b>");
		};
		OnSensationSent = delegate(int sensID, OWOMuscles muscle)
		{
			OnLog?.Invoke("SensationSend with sensation id: <b>" + sensID + "</b> and OWOMuscle: <b>" + muscle.ToString() + "</b>");
		};
		OnLog = delegate(string message)
		{
			UniLog.Log("<b>[OWOConnectionHandler] Log:</b> " + message);
		};
	}

	private void SendMessageToOWOApp(string message)
	{
		try
		{
			byte[] bytes = Encoding.ASCII.GetBytes(message);
			stream.Write(bytes, 0, bytes.Length);
			OnLog?.Invoke("<Log> [OWOTcpClient] A message sent to server: " + message);
		}
		catch (ArgumentNullException ex)
		{
			OnLog?.Invoke("<Error> [OWOTcpClient] ArgumentNullException on 'SendMessageToOWOApp' with message: " + message + ". " + ex);
		}
		catch (SocketException ex2)
		{
			OnLog?.Invoke("<Error> [OWOTcpClient] SocketException on 'SendMessageToOWOApp': with message: " + message + ". " + ex2);
		}
		catch (NullReferenceException)
		{
			OnLog?.Invoke("<Error> [OWOTcpClient] NullReferenceException on 'SendMessageToOWOApp' with message: " + message + ".");
		}
	}

	/// <summary>
	/// Connect to OWO app. Note: for this client to work, you need to provide the IP of a device running OWO app
	/// </summary>
	/// <param name="_ip"></param>
	public void Connect(string _ip)
	{
		ip = _ip;
		try
		{
			TcpClient tcpClient = new TcpClient(ip, port);
			stream = tcpClient.GetStream();
			OnConnectionOpen?.Invoke();
		}
		catch (ArgumentNullException ex)
		{
			OnLog?.Invoke("<Error> [OWOTcpClient] ArgumentNullException on 'Connect': " + ex);
		}
		catch (SocketException ex2)
		{
			OnLog?.Invoke("<Error> [OWOTcpClient] SocketException on 'Connect': " + ex2);
		}
	}

	/// <summary>
	/// Request to OWO app a sensation to be runned
	/// </summary>
	/// <param name="sensationID"> Id of the sensation to run </param>
	/// <param name="muscle"> Muscle to focus </param>
	public void SendSensation(int sensationID, OWOMuscles muscle)
	{
		string[] obj = new string[5]
		{
			"owo/",
			sensationID.ToString(),
			"/",
			null,
			null
		};
		int num = (int)muscle;
		obj[3] = num.ToString();
		obj[4] = "/eof";
		string message = string.Concat(obj);
		SendMessageToOWOApp(message);
		OnSensationSent?.Invoke(sensationID, muscle);
	}

	/// <summary>
	/// Close conection, to finally close the conection a second is waited
	/// </summary>
	public void Close()
	{
		SendMessageToOWOApp("Close");
		Task.Delay(1000).ContinueWith(delegate
		{
			stream?.Close();
			client?.Close();
			OnConnectionClosed?.Invoke();
		});
	}
}
[DataModelType]
