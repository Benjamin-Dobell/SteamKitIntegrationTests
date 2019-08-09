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

	public class LobbyStateFixture
	{
		public int LookupIdentifier { get; set; }
		public SteamID LobbyId { get; set; }
	}

	[TestCaseOrderer(nameof(SteamKitIntegrationTests) + "." + nameof(SequentialTestCaseOrderer), "SteamKitIntegrationTests")]
	public class MatchmakingTests : IClassFixture<PrimaryUserFixture>, IClassFixture<SecondaryUserFixture>, IClassFixture<LobbyStateFixture>
	{
		private PrimaryUserFixture Primary { get; }
		private SecondaryUserFixture Secondary { get; }
		private LobbyStateFixture LobbyStateFixture { get; }

		private int LookupIdentifier
		{
			get => LobbyStateFixture.LookupIdentifier;
			set => LobbyStateFixture.LookupIdentifier = value;
		}

		private SteamID LobbyId
		{
			get => LobbyStateFixture.LobbyId;
			set => LobbyStateFixture.LobbyId = value;
		}

		public MatchmakingTests(PrimaryUserFixture primaryUserFixture, SecondaryUserFixture secondaryUserFixture, LobbyStateFixture lobbyStateFixture)
		{
			Primary = primaryUserFixture;
			Secondary = secondaryUserFixture;
			LobbyStateFixture = lobbyStateFixture;
		}

		#region Single user tests

		[OrderedFact]
		private void CanCreateLobby()
		{
			Primary.Matchmaking.CreateLobby(Config.AppId, ELobbyType.FriendsOnly, 10);
			Primary.WaitForCallback(Config.DefaultHandlerTimeout, (SteamMatchmaking.CreateLobbyCallback c) => {
				Assert.Equal(EResult.OK, c.Result);
				LobbyId = c.LobbySteamID;
			});
		}

		[OrderedFact]
		private void CanSetLobbyData()
		{
			Primary.Matchmaking.SetLobbyData(Config.AppId, LobbyId, ELobbyType.Invisible, 5, 0, new Dictionary<string, string> {
				{ "meta", "123" },
			});

			Primary.WaitForCallback(
				Config.DefaultHandlerTimeout,
				(SteamMatchmaking.SetLobbyDataCallback c) => {
					Assert.Equal(EResult.OK, c.Result);
					Assert.Equal(Config.AppId, c.AppID);
					Assert.Equal(LobbyId, c.LobbySteamID);

					var cachedLobby = Primary.Matchmaking.GetLobby(Config.AppId, LobbyId);
					Assert.NotNull(cachedLobby);
					Assert.Equal(LobbyId, cachedLobby.SteamID);
					Assert.Equal(Primary.Client.SteamID, cachedLobby.OwnerSteamID);
					Assert.Equal(ELobbyType.Invisible, cachedLobby.LobbyType);
					Assert.Equal(5, cachedLobby.MaxMembers);
					Assert.Equal(1, cachedLobby.NumMembers);
					Assert.Equal(1, cachedLobby.Members.Count);
					Assert.Equal("123", cachedLobby.Metadata["meta"]);
				}
			);

			Primary.WaitForCallback(
				Config.DefaultHandlerTimeout,
				(SteamMatchmaking.LobbyDataCallback c) => {
					var lobby = c.Lobby;
					Assert.Equal(Config.AppId, c.AppID);
					Assert.NotNull(lobby);
					Assert.Equal(LobbyId, lobby.SteamID);
					Assert.Equal(Primary.Client.SteamID, lobby.OwnerSteamID);
					Assert.Equal(ELobbyType.Invisible, lobby.LobbyType);
					Assert.Equal(5, lobby.MaxMembers);
					Assert.Equal(1, lobby.NumMembers);
					Assert.Equal(1, lobby.Members.Count);
					Assert.Equal("123", lobby.Metadata["meta"]);
				}
			);
		}

		[OrderedFact]
		private void CanReceiveLobbyDataUpdates()
		{
			Primary.Matchmaking.SetLobbyData(Config.AppId, LobbyId, ELobbyType.Public, 10, 0, new Dictionary<string, string> {
				{ "meta", "456" },
			});

			Primary.WaitForCallbacks(
				Config.DefaultHandlerTimeout,
				(SteamMatchmaking.SetLobbyDataCallback c) => {
					Assert.Equal(EResult.OK, c.Result);
					Assert.Equal(Config.AppId, c.AppID);
					Assert.Equal(LobbyId, c.LobbySteamID);

					var cachedLobby = Primary.Matchmaking.GetLobby(Config.AppId, LobbyId);
					Assert.NotNull(cachedLobby);
					Assert.Equal(LobbyId, cachedLobby.SteamID);
					Assert.Equal(Primary.Client.SteamID, cachedLobby.OwnerSteamID);
					Assert.Equal(ELobbyType.Public, cachedLobby.LobbyType);
					Assert.Equal(10, cachedLobby.MaxMembers);
					Assert.Equal(1, cachedLobby.NumMembers);
					Assert.Equal(1, cachedLobby.Members.Count);
					Assert.Equal("456", cachedLobby.Metadata["meta"]);
				},
				(SteamMatchmaking.LobbyDataCallback c) => {
					var lobby = c.Lobby;
					Assert.Equal(Config.AppId, c.AppID);
					Assert.NotNull(lobby);
					Assert.Equal(LobbyId, lobby.SteamID);
					Assert.Equal(Primary.Client.SteamID, lobby.OwnerSteamID);
					Assert.Equal(ELobbyType.Public, lobby.LobbyType);
					Assert.Equal(10, lobby.MaxMembers);
					Assert.Equal(1, lobby.NumMembers);
					Assert.Equal(1, lobby.Members.Count);
					Assert.Equal("456", lobby.Metadata["meta"]);
				}
			);
		}

		[OrderedFact]
		private void CanRetrieveCreatedLobbyFromLobbyList()
		{
			Thread.Sleep(750); // Give Steam a bit of time publicly list our lobby

			Primary.Matchmaking.GetLobbyList(Config.AppId, new List<SteamMatchmaking.Lobby.Filter>() {
				new SteamMatchmaking.Lobby.StringFilter("meta", ELobbyComparison.Equal, "456")
			}, 1);

			Primary.WaitForCallback(Config.DefaultHandlerTimeout, (SteamMatchmaking.GetLobbyListCallback c) => {
				Assert.Equal(EResult.OK, c.Result);
				Assert.Equal(Config.AppId, c.AppID);
				Assert.Single(c.Lobbies);

				var lobby = c.Lobbies[0];

				Assert.NotNull(lobby);
				Assert.Equal(LobbyId, lobby.SteamID);
				Assert.Equal(ELobbyType.Public, lobby.LobbyType);
				Assert.Equal(10, lobby.MaxMembers);
				Assert.Equal(1, lobby.NumMembers);
				Assert.Equal(1, lobby.Members.Count); // We're still in the lobby, so we expect member details.
				Assert.Equal("456", lobby.Metadata["meta"]);
				Assert.Equal(0, lobby.Weight);
				Assert.Equal(0, lobby.Distance);
			});
		}

		#endregion

		#region User interaction tests

		[OrderedFact]
		private void CanCreatePublicLobby()
		{
			LookupIdentifier = new Random().Next();

			Primary.Matchmaking.CreateLobby(Config.AppId, ELobbyType.Public, 10, 0, new Dictionary<string, string> {
				{ "lookup", LookupIdentifier.ToString() },
			});

			Primary.WaitForCallback(
				Config.DefaultHandlerTimeout,
				(SteamMatchmaking.CreateLobbyCallback c) => {
					Assert.Equal(EResult.OK, c.Result);
					LobbyId = c.LobbySteamID;

					var lobby = Primary.Matchmaking.GetLobby(Config.AppId, LobbyId);
					Assert.Equal(Primary.Client.SteamID, lobby.OwnerSteamID);
					Assert.Equal(ELobbyType.Public, lobby.LobbyType);
					Assert.Equal(10, lobby.MaxMembers);
					Assert.Equal(0, lobby.LobbyFlags);
					Assert.Equal(LookupIdentifier.ToString(), lobby.Metadata["lookup"]);
					Assert.Equal(1, lobby.Members.Count);
					Assert.Equal(Primary.Client.SteamID, lobby.Members[0].SteamID);
				}
			);
		}

		[OrderedFact]
		private void CanListOtherUserLobbies()
		{
			Thread.Sleep(750); // Give Steam a bit of time publicly list our lobby

			Secondary.Matchmaking.GetLobbyList(Config.AppId, new List<SteamMatchmaking.Lobby.Filter>() {
				new SteamMatchmaking.Lobby.NumericalFilter("lookup", ELobbyComparison.Equal, LookupIdentifier),
			}, 1);

			Secondary.WaitForCallback(Config.DefaultHandlerTimeout, (SteamMatchmaking.GetLobbyListCallback c) => {
				Assert.Equal(EResult.OK, c.Result);
				Assert.Equal(Config.AppId, c.AppID);
				Assert.Single(c.Lobbies);

				var lobby = c.Lobbies[0];

				Assert.Equal(LobbyId, lobby.SteamID);
				Assert.Equal(ELobbyType.Public, lobby.LobbyType);
				Assert.Equal(10, lobby.MaxMembers);
				Assert.Equal(1, lobby.NumMembers);
				Assert.Equal(0, lobby.Members.Count);
				Assert.Equal(LookupIdentifier.ToString(), lobby.Metadata["lookup"]);
				Assert.Equal(0, lobby.Weight);
				Assert.Equal(0, lobby.Distance);
			});
		}

		[OrderedFact]
		private void CanJoinLobby()
		{
			Thread.Sleep(500);
			Secondary.Matchmaking.JoinLobby(Config.AppId, LobbyId);

			Secondary.WaitForCallback(Config.DefaultHandlerTimeout, (SteamMatchmaking.JoinLobbyCallback c) => {
				var lobby = c.Lobby;
				Assert.Equal(EChatRoomEnterResponse.Success, c.ChatRoomEnterResponse);
				Assert.Equal(Config.AppId, c.AppID);
				Assert.NotNull(lobby);
				Assert.Equal(LobbyId, lobby.SteamID);
				Assert.Equal(Primary.Client.SteamID, lobby.OwnerSteamID);
				Assert.Equal(2, lobby.NumMembers);
				Assert.Equal(2, lobby.Members.Count);

				var cachedLobby = Secondary.Matchmaking.GetLobby(Config.AppId, LobbyId);
				Assert.Equal(2, cachedLobby.Members.Count);
				Assert.NotNull(cachedLobby.Members.First(m => m.SteamID == Secondary.Client.SteamID));
			});
		}

		[OrderedFact]
		private void CanTellOtherUserJoinedLobby()
		{
			Primary.WaitForCallback(Config.DefaultHandlerTimeout, (SteamMatchmaking.UserJoinedLobbyCallback c) => {
				var user = c.User;
				Assert.Equal(Config.AppId, c.AppID);
				Assert.Equal(LobbyId, c.LobbySteamID);
				Assert.NotNull(user);
				Assert.Equal(Secondary.Client.SteamID, user.SteamID);
				Assert.Equal(Secondary.Friends.GetPersonaName(), user.PersonaName);
			});
		}

		[OrderedFact]
		private void CanSetOwnMemberMetadata()
		{
			Secondary.Matchmaking.SetLobbyMemberData(Config.AppId, LobbyId, new Dictionary<string, string> {
				{ "meta", "data" }
			});

			Secondary.WaitForCallback(Config.DefaultHandlerTimeout, (SteamMatchmaking.SetLobbyDataCallback c) => {
				Assert.Equal(EResult.OK, c.Result);
				Assert.Equal(Config.AppId, c.AppID);
				Assert.Equal(LobbyId, c.LobbySteamID);

				var cachedLobby = Secondary.Matchmaking.GetLobby(Config.AppId, LobbyId);
				var cachedUser = cachedLobby.Members.First(member => member.SteamID == Secondary.Client.SteamID);
				Assert.Equal("data", cachedUser.Metadata["meta"]);
			});

			Secondary.WaitForCallback(Config.DefaultHandlerTimeout, (SteamMatchmaking.LobbyDataCallback c) => {
				var lobby = c.Lobby;
				Assert.Equal(Config.AppId, c.AppID);
				Assert.Equal(LobbyId, lobby.SteamID);

				var user = lobby.Members.First(member => member.SteamID == Secondary.Client.SteamID);
				Assert.Equal("data", user.Metadata["meta"]);
			});
		}

		[OrderedFact]
		private void CanSeeOtherUserMetadataUpdates()
		{
			Primary.WaitForCallback(Config.DefaultHandlerTimeout, (SteamMatchmaking.LobbyDataCallback c) => {
				var lobby = c.Lobby;
				Assert.Equal(Config.AppId, c.AppID);
				Assert.Equal(LobbyId, lobby.SteamID);
				Assert.Equal(2, lobby.NumMembers);
				Assert.Equal(2, lobby.Members.Count);

				var user = lobby.Members.First(member => member.SteamID == Secondary.Client.SteamID);
				Assert.Equal("data", user.Metadata["meta"]);
			});
		}

		[OrderedFact]
		private void CanSetLobbyOwner()
		{
			Primary.Matchmaking.SetLobbyOwner(Config.AppId, LobbyId, Secondary.Client.SteamID);

			Primary.WaitForCallback(Config.DefaultHandlerTimeout, (SteamMatchmaking.SetLobbyOwnerCallback c) => {
				Assert.Equal(EResult.OK, c.Result);
				Assert.Equal(Config.AppId, c.AppID);
				Assert.Equal(LobbyId, c.LobbySteamID);

				var cachedLobby = Primary.Matchmaking.GetLobby(Config.AppId, LobbyId);
				Assert.Equal(Secondary.Client.SteamID, cachedLobby.OwnerSteamID);
			});
		}

		[OrderedFact]
		private void CanTellWhenLobbyOwnershipChanges()
		{
			Primary.WaitForCallback(Config.DefaultHandlerTimeout, (SteamMatchmaking.LobbyDataCallback c) => {
				var lobby = c.Lobby;
				Assert.Equal(Config.AppId, c.AppID);
				Assert.Equal(LobbyId, lobby.SteamID);
				Assert.Equal(Secondary.Client.SteamID, lobby.OwnerSteamID);
			});

			Secondary.WaitForCallback(Config.DefaultHandlerTimeout, (SteamMatchmaking.LobbyDataCallback c) => {
				var lobby = c.Lobby;
				Assert.Equal(Config.AppId, c.AppID);
				Assert.Equal(LobbyId, lobby.SteamID);
				Assert.Equal(Secondary.Client.SteamID, lobby.OwnerSteamID);
			});
		}

		[OrderedFact]
		private void CanLeaveLobby()
		{
			Primary.Matchmaking.LeaveLobby(Config.AppId, LobbyId);

			Primary.WaitForCallback(Config.DefaultHandlerTimeout, (SteamMatchmaking.LeaveLobbyCallback c) => {
				Assert.Equal(EResult.OK, c.Result);
				Assert.Equal(Config.AppId, c.AppID);
				Assert.Equal(LobbyId, c.LobbySteamID);

				var cachedLobby = Primary.Matchmaking.GetLobby(Config.AppId, LobbyId);
				Assert.Empty(cachedLobby.Members); // We only have membership information for lobbies we occupy
			});
		}

		[OrderedFact]
		private void CanSeeOtherUserLeftLobby()
		{
			Secondary.WaitForCallback(Config.DefaultHandlerTimeout, (SteamMatchmaking.UserLeftLobbyCallback c) => {
				var user = c.User;

				Assert.Equal(Config.AppId, c.AppID);
				Assert.Equal(LobbyId, c.LobbySteamID);
				Assert.NotNull(user);
				Assert.Equal(Primary.Client.SteamID, user.SteamID);

				var cachedLobby = Secondary.Matchmaking.GetLobby(Config.AppId, LobbyId);
				Assert.Single(cachedLobby.Members);
				Assert.Equal(Secondary.Client.SteamID, cachedLobby.Members[0].SteamID);
			});
		}

		#endregion
	}
}
