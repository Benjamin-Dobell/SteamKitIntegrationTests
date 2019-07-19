using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

	public class SingleUserTests : IClassFixture<PrimaryUserFixture>
	{
		private const uint AppId = 250820; // SteamVR

		private SteamUserFixture Steam { get; }

		public SingleUserTests(PrimaryUserFixture fixture)
		{
			Steam = fixture;
		}
	}

	[Collection("Users")]
	public class DualUserTests : IClassFixture<PrimaryUserFixture>, IClassFixture<SecondaryUserFixture>
	{
		SteamUserFixture Primary { get; }
		SteamUserFixture Secondary { get; }

		public DualUserTests(PrimaryUserFixture primaryUserFixture, SecondaryUserFixture secondaryUserFixture)
		{
			Primary = primaryUserFixture;
			Secondary = secondaryUserFixture;
		}
	}
}