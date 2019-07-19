using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using Xunit;

namespace SteamKitIntegrationTests
{
	public class SteamUserFixture : IAsyncLifetime
	{
		private enum State
		{
			Connecting,
			Connected,
			LoggedOn,
			LoggedOut,
			Disconnecting,
			Disconnected,
		}

		private readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
		private readonly TimeSpan LogonTimeout = TimeSpan.FromSeconds(5);
		private readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds(10);

		public SteamClient Client { get; } = new SteamClient();
		public SteamUser User { get; }
		public SteamMatchmaking Matchmaking { get; }
		public CallbackManager CallbackManager { get; }

		private State state;

		private SteamUser.LogOnDetails logOnDetails;

		public SteamUserFixture(SteamUser.LogOnDetails logOnDetails)
		{
			this.logOnDetails = logOnDetails;

			User = Client.GetHandler<SteamUser>();
			Matchmaking = Client.GetHandler<SteamMatchmaking>();

			CallbackManager = new CallbackManager(Client);

			CallbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
			CallbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

			CallbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
			CallbackManager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
		}

		public Task InitializeAsync()
		{
			return Task.Run(() => {
				DateTime connectionCommencement = DateTime.Now;

				Client.Connect();

				while (state != State.Connected)
				{
					TimeSpan elapsedTime = DateTime.Now.Subtract(connectionCommencement);

					if (elapsedTime.CompareTo(ConnectTimeout) >= 0)
					{
						throw new TimeoutException("Timed out connecting to Steam");
					}

					CallbackManager.RunWaitAllCallbacks(ConnectTimeout.Subtract(elapsedTime));
				}

				DateTime logonCommencement = DateTime.Now;

				User.LogOn(logOnDetails);

				while (state == State.Connected)
				{
					TimeSpan elapsedTime = DateTime.Now.Subtract(logonCommencement);

					if (elapsedTime.CompareTo(LogonTimeout) >= 0)
					{
						throw new TimeoutException("Timed out logging into Steam");
					}

					CallbackManager.RunWaitAllCallbacks(LogonTimeout.Subtract(elapsedTime));
				}

				if (state != State.LoggedOn)
				{
					throw new Exception($"Failed to login to Steam account: ${logOnDetails.Username}");
				}
			});
		}

		public Task DisposeAsync()
		{
			return Task.Run(() => {
				if (state == State.Disconnected)
				{
					return;
				}

				state = State.Disconnecting;

				DateTime disconnectCommencement = DateTime.Now;

				if (Client.CellID != null)
				{
					User.LogOff();
				}
				else
				{
					Client.Disconnect();
				}

				while (state != State.Disconnected)
				{
					TimeSpan elapsedTime = DateTime.Now.Subtract(disconnectCommencement);

					if (elapsedTime.CompareTo(DisconnectTimeout) >= 0)
					{
						throw new TimeoutException("Timed out disconnecting from Steam");
					}

					CallbackManager.RunWaitAllCallbacks(DisconnectTimeout.Subtract(elapsedTime));
				}
			});
		}

		public void WaitForCallback<TSuccess>(TimeSpan timeout, Action<TSuccess> success)
			where TSuccess : class, ICallbackMsg
		{
			bool finished = false;

			var successSubscription = CallbackManager.Subscribe((TSuccess s) => {
				finished = true;
				success(s);
			});

			try
			{
				DateTime commencement = DateTime.Now;

				var callbackType = success.Method.GetParameters().Select(pi => pi.ParameterType).First();

				while (!finished)
				{
					if (state != State.LoggedOn)
					{
						throw new Exception($"Steam {state} whilst waiting for {callbackType} callback");
					}

					var elapsedTime = DateTime.Now.Subtract(commencement);

					if (elapsedTime.CompareTo(timeout) >= 0)
					{
						throw new TimeoutException($"Timed out waiting for {callbackType} callback");
					}

					CallbackManager.RunWaitAllCallbacks(timeout.Subtract(elapsedTime));
				}
			}
			finally
			{
				successSubscription.Dispose();
			}
		}

		public void WaitForCallback<TSuccess, TFailure>(TimeSpan timeout, Action<TSuccess> success, Action<TFailure> failure)
			where TSuccess : class, ICallbackMsg
			where TFailure : class, ICallbackMsg
		{
			bool finished = false;

			var successSubscription = CallbackManager.Subscribe((TSuccess s) => {
				finished = true;
				success(s);
			});

			var failureSubscription = CallbackManager.Subscribe((TFailure f) => {
				finished = true;
				failure(f);
			});

			try
			{
				DateTime commencement = DateTime.Now;

				var callbackType = success.Method.GetParameters().Select(pi => pi.ParameterType).First();

				while (!finished)
				{
					if (state != State.LoggedOn)
					{
						throw new Exception($"Steam disconnected whilst waiting for {callbackType} callback");
					}

					var elapsedTime = DateTime.Now.Subtract(commencement);

					if (elapsedTime.CompareTo(timeout) >= 0)
					{
						throw new TimeoutException($"Timed out waiting for {callbackType} callback");
					}

					CallbackManager.RunWaitAllCallbacks(timeout.Subtract(elapsedTime));
				}
			}
			finally
			{
				successSubscription.Dispose();
				failureSubscription.Dispose();
			}
		}

		private void OnConnected(SteamClient.ConnectedCallback connectedCallback)
		{
			state = State.Connected;
		}

		private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
		{
			state = State.LoggedOn;
		}

		private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
		{
			state = State.LoggedOut;
		}

		void OnDisconnected(SteamClient.DisconnectedCallback disconnectedCallback)
		{
			state = State.Disconnected;
		}

		protected static SteamUser.LogOnDetails ReadCredentials(string userPrefix)
		{
			try
			{
				var s = Path.DirectorySeparatorChar;
				DotNetEnv.Env.Load($"..{s}..{s}..{s}.env");
			}
			catch (Exception e)
			{
				// '.env' files are only used during development, and they're optional.
			}

			var logOnDetails = new SteamUser.LogOnDetails { Username = Environment.GetEnvironmentVariable($"{userPrefix}_STEAM_USER") };

			if (logOnDetails.Username == null)
			{
				throw new Exception($"The {userPrefix}_STEAM_USER environment variable must be specified");
			}

			logOnDetails.LoginKey = Environment.GetEnvironmentVariable($"{userPrefix}_STEAM_KEY");

			if (logOnDetails.LoginKey == null)
			{
				logOnDetails.Password = Environment.GetEnvironmentVariable($"{userPrefix}_STEAM_PASSWORD");

				if (logOnDetails.Password == null)
				{
					throw new Exception($"Either the {userPrefix}_STEAM_KEY or {userPrefix}_STEAM_PASSWORD environment variable must be specified");
				}
			}

			return logOnDetails;
		}
	}
}