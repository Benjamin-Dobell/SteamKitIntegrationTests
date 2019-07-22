FROM mcr.microsoft.com/dotnet/core/sdk:2.2

RUN set -x \
	&& apt-get update -y \
	&& apt-get install -y --no-install-recommends --no-install-suggests \
		git \
    && apt-get clean

WORKDIR /app/SteamKitIntegrationTests
COPY SteamKitIntegrationTests/SteamKitIntegrationTests.csproj ./
COPY SteamKitIntegrationTests/*.cs ./

WORKDIR /app
COPY run.sh ./

ENTRYPOINT ["/app/run.sh"]
