using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SteamKit2;
using Xunit;

namespace SteamKitIntegrationTests
{
	public class PrimaryUserFixture : SteamUserFixture
	{
		public PrimaryUserFixture() : base(ReadCredentials("PRIMARY"))
		{
		}
	}

	public class SecondaryUserFixture : SteamUserFixture
	{
		public SecondaryUserFixture() : base(ReadCredentials("SECONDARY"))
		{
		}
	}

	[TestCaseOrderer(nameof(SteamKitIntegrationTests) + "." + nameof(SequentialTestCaseOrderer), "SteamKitIntegrationTests")]
	public class MatchmakingTests : IClassFixture<PrimaryUserFixture>, IClassFixture<SecondaryUserFixture>
	{
		private PrimaryUserFixture Primary { get; }
		private SecondaryUserFixture Secondary { get; }

		public MatchmakingTests(PrimaryUserFixture primaryUserFixture, SecondaryUserFixture secondaryUserFixture)
		{
			Primary = primaryUserFixture;
			Secondary = secondaryUserFixture;
		}

		private const string META_VALUE = "meta123";

		[OrderedFact]
		private async void CanLobbyStuffLikeAPro()
		{
			var createLobby = await Primary.Matchmaking.CreateLobby(Config.AppId, ELobbyType.Public, 2);

			Assert.Equal(EResult.OK, createLobby.Result);

			var lobbyId = createLobby.LobbySteamID;

			var setLobbyMeta = await Primary.Matchmaking.SetLobbyData(Config.AppId, lobbyId, ELobbyType.Public, 2, 0, new Dictionary<string, string> {
				{ "meta", META_VALUE },
			});

			Assert.Equal(EResult.OK, setLobbyMeta.Result);

			Thread.Sleep(1000); // Give Steam some time to propagate public lobby info

			var lobbyList = await Secondary.Matchmaking.GetLobbyList(Config.AppId, new List<SteamMatchmaking.Lobby.Filter>() {
				new SteamMatchmaking.Lobby.StringFilter("meta", ELobbyComparison.Equal, META_VALUE)
			}, 1);

			Assert.Equal(EResult.OK, lobbyList.Result);

			var listedLobby = lobbyList.Lobbies.FirstOrDefault();

			Assert.NotNull(listedLobby);
			Assert.Equal(listedLobby.SteamID, lobbyId);

			var getLobbyData = await Secondary.Matchmaking.GetLobbyData(Config.AppId, listedLobby.SteamID);

			var lobby = getLobbyData.Lobby;

			Assert.Equal(lobbyId, lobby.SteamID);
			Assert.Equal(Primary.Client.SteamID, lobby.OwnerSteamID);
			Assert.Equal(2, lobby.MaxMembers);
			Assert.Equal(1, lobby.NumMembers);

			var shrinkLobbySize = await Primary.Matchmaking.SetLobbyData(Config.AppId, lobbyId, ELobbyType.Public, 1, 0, new Dictionary<string, string> {
				{ "meta", META_VALUE },
			});

			Assert.Equal(EResult.OK, shrinkLobbySize.Result);

			Thread.Sleep(1000); // Give Steam some time to propagate public lobby info

			var blankLobbyList = await Secondary.Matchmaking.GetLobbyList(Config.AppId, new List<SteamMatchmaking.Lobby.Filter>() {
				new SteamMatchmaking.Lobby.StringFilter("meta", ELobbyComparison.Equal, META_VALUE)
			}, 1);

			Assert.Equal(EResult.OK, blankLobbyList.Result);
			Assert.Empty(blankLobbyList.Lobbies);

			var getUpdatedLobbyData = await Secondary.Matchmaking.GetLobbyData(Config.AppId, listedLobby.SteamID);

			var updatedLobby = getUpdatedLobbyData.Lobby;

			Assert.Equal(lobbyId, updatedLobby.SteamID);
			Assert.Equal(Primary.Client.SteamID, updatedLobby.OwnerSteamID);
			Assert.Equal(1, updatedLobby.MaxMembers);
			Assert.Equal(1, updatedLobby.NumMembers);

			var leaveLobby = await Primary.Matchmaking.LeaveLobby(Config.AppId, lobbyId);

			Assert.Equal(EResult.OK, leaveLobby.Result);

			Secondary.WaitForCallback(Config.DefaultHandlerTimeout, (SteamMatchmaking.LobbyDataCallback c) => {
				var destroyedLobby = c.Lobby;

				Assert.Equal(lobbyId, destroyedLobby.SteamID);
				Assert.Equal(new SteamID(), destroyedLobby.OwnerSteamID);
			});

			// The following GetLobbyData() is not allowed as the lobby has been deleted. Attempting it will result in the
			// Steam backend disconnecting us.
			//
			//var getDestroyedLobbyData = await Secondary.Matchmaking.GetLobbyData(Config.AppId, listedLobby.SteamID);
			//
			// You have to listen for updates, as above. In this situation we knew the lobby was deleted (locally by the primary
			// user), so we knew to wait for the lobby data callback.
			//
			// In practice, we aren't going to know when other users have deleted a lobby and we may attempt to GetLobbyData()
			// just as the lobby is being deleted (before we've received LobbyDataCallback locally). This is probably an
			// insurmountable problem based on the current event-driven (multi-threaded) architecture of SteamKit2.
		}
	}
}
