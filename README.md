# SteamKit2 Integration Tests

These tests are best run in the provided Docker container, as it's configurable and facilitates running against arbitrary SteamKit versions.

# Build

```bash
docker build -f dockerfiles/Dockerfile -t steamkit2_tests .
```

# Running

## Environment variables

| Variable | Default | Description |
| --- | --- | --- |
| STEAMKIT_REPO | https://github.com/SteamRE/SteamKit.git | SteamKit Git repository to clone and run the tests against. |
| STEAMKIT_REF | master | Git commit ref to checkout and build against. |
| PRIMARY_STEAM_USER | N/A | Username of the primary Steam account. |
| PRIMARY_STEAM_KEY | N/A | Auth key of the primary Steam account. |
| PRIMARY_STEAM_PASSWORD | N/A | Password of the primary Steam account. |
| PRIMARY_STEAM_GUARD_CODE | N/A | Steam guard code of the primary Steam account. |
| SECONDARY_STEAM_USER | N/A | Username of the secondary Steam account. |
| SECONDARY_STEAM_KEY | N/A | Auth key of the secondary Steam account. |
| SECONDARY_STEAM_PASSWORD | N/A | Password of the secondary Steam account. |
| SECONDARY_STEAM_GUARD_CODE | N/A | Steam guard of the secondary Steam account. |

Steam keys are preferred, and will take precedence over passwords (and Steam guard codes) if both are provided.

If a key is not provided, then one will be printed before the tests run. This may be used for subsequent runs.

## Docker

Steam credentials must be provided in accordance with the environment variables documented above.

e.g.

```bash
docker run -e PRIMARY_STEAM_USER=account_1 -e PRIMARY_STEAM_KEY=key_1 -e SECONDARY_STEAM_USER=account_2 -e SECONDARY_STEAM_KEY=key_2 steamkit2_tests
```

# Development

For building and running outside docker clone, symlink or otherwise copy, SteamKit into a directory named `SteamKit` at the root of this repository.

We're intentionally not referencing SteamKit as a sub-module as we don't want to be tied to a specific SteamKit version.
