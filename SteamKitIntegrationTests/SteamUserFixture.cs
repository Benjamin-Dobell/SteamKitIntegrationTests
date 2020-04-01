using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.Internal;
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

		private readonly TimeSpan connectTimeout = TimeSpan.FromSeconds(5);
		private readonly TimeSpan logonTimeout = TimeSpan.FromSeconds(10);
		private readonly TimeSpan disconnectTimeout = TimeSpan.FromSeconds(10);

		public SteamClient Client { get; } = new SteamClient();

		public SteamFriends Friends { get; }
		public SteamMatchmaking Matchmaking { get; }
		public CallbackManager CallbackManager { get; }

		private SteamUser User { get; }

		private State state;

		private SteamUser.LogOnDetails logOnDetails;

		public SteamUserFixture(SteamUser.LogOnDetails logOnDetails)
		{
			this.logOnDetails = logOnDetails;

			Friends = Client.GetHandler<SteamFriends>();
			Matchmaking = Client.GetHandler<SteamMatchmaking>();
			User = Client.GetHandler<SteamUser>();

			CallbackManager = new CallbackManager(Client);

			CallbackManager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
			CallbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
		}

		public Task InitializeAsync()
		{
			return Task.Run(() => {
				Client.Connect();

				WaitForCallback(new[] { State.Connecting }, connectTimeout, (SteamClient.ConnectedCallback c) => { state = State.Connected; });

				User.LogOn(logOnDetails);

				var loggedOnCallback = new Action<SteamUser.LoggedOnCallback>((SteamUser.LoggedOnCallback c) => {
					if (c.Result == EResult.OK)
					{
						Console.WriteLine($"{logOnDetails.Username} logged on");
						state = State.LoggedOn;
					}
					else
					{
						Console.Error.WriteLine($"{logOnDetails.Username} failed with result: {c.Result}");
						throw new Exception($"{logOnDetails.Username} failed with result: {c.Result}");
					}
				});

				if (logOnDetails.Password != null)
				{
					WaitForCallbacks(
						new[] { State.Connected, State.LoggedOn },
						logonTimeout,
						loggedOnCallback,
						(SteamUser.LoginKeyCallback c) => {
							Console.WriteLine($"{Friends.GetPersonaName()} login key: {c.LoginKey}");
						}
					);
				}
				else
				{
					WaitForCallback(
						new[] { State.Connected },
						logonTimeout,
						loggedOnCallback
					);
				}

				var appUsageEvent = new ClientMsg<MsgClientAppUsageEvent>() {
					Body = {
						AppUsageEvent = EAppUsageEvent.GameLaunch,
						GameID = Config.AppId,
					}
				};

				Client.Send(appUsageEvent);

				var gamesPlayed = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
				gamesPlayed.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed {
					game_id = new GameID(Config.AppId),
				});
				Client.Send(gamesPlayed);

				Friends.SetPersonaState(EPersonaState.Online);
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

					if (elapsedTime.CompareTo(disconnectTimeout) >= 0)
					{
						throw new TimeoutException("Timed out disconnecting from Steam");
					}

					CallbackManager.RunWaitAllCallbacks(disconnectTimeout.Subtract(elapsedTime));
				}
			});
		}


		public void WaitForCallback<TCallback>(TimeSpan timeout, Action<TCallback> action)
			where TCallback : class, ICallbackMsg
		{
			WaitForCallback(new[] { State.LoggedOn }, timeout, action);
		}

		public void WaitForCallbacks<TCallback1, TCallback2>(TimeSpan timeout, Action<TCallback1> action1, Action<TCallback2> action2)
			where TCallback1 : class, ICallbackMsg
			where TCallback2 : class, ICallbackMsg
		{
			WaitForCallbacks(new[] { State.LoggedOn }, timeout, action1, action2);
		}

		private void WaitForCallback<TCallback>(State[] expectedStates, TimeSpan timeout, Action<TCallback> action)
			where TCallback : class, ICallbackMsg
		{
			bool finished = false;

			var callbackSubscription = CallbackManager.Subscribe((TCallback c) => {
				finished = true;
				action(c);
			});

			try
			{
				DateTime commencement = DateTime.Now;

				var callbackType = action.Method.GetParameters().Select(pi => pi.ParameterType).First();

				while (!finished)
				{
					if (!expectedStates.Contains(state))
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
				callbackSubscription.Dispose();
			}
		}

		private void WaitForCallbacks<TCallback1, TCallback2>(State[] expectedStates, TimeSpan timeout, Action<TCallback1> action1, Action<TCallback2> action2)
			where TCallback1 : class, ICallbackMsg
			where TCallback2 : class, ICallbackMsg
		{
			bool finished1 = false;
			bool finished2 = false;

			var callback1Subscription = CallbackManager.Subscribe((TCallback1 c) => {
				finished1 = true;
				action1(c);
			});

			var callback2Subscription = CallbackManager.Subscribe((TCallback2 c) => {
				finished2 = true;
				action2(c);
			});

			try
			{
				DateTime commencement = DateTime.Now;

				var callbackType = action1.Method.GetParameters().Select(pi => pi.ParameterType).First();

				while (!finished1 || !finished2)
				{
					if (!expectedStates.Contains(state))
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
				callback1Subscription.Dispose();
				callback2Subscription.Dispose();
			}
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
			catch (Exception)
			{
				// '.env' files are only used during development, and they're optional.
			}

			var logOnDetails = new SteamUser.LogOnDetails {
				Username = Environment.GetEnvironmentVariable($"{userPrefix}_STEAM_USER"),
				ShouldRememberPassword = true,
			};

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

				logOnDetails.TwoFactorCode = Environment.GetEnvironmentVariable($"{userPrefix}_STEAM_GUARD_CODE");
			}

			return logOnDetails;
		}
	}
}
