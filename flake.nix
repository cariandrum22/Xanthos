{
  description = "Development environment for the Xanthos F# library";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs =
    {
      self,
      nixpkgs,
      flake-utils,
    }:
    flake-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = import nixpkgs { inherit system; };
        sdkVersion = (builtins.fromJSON (builtins.readFile ./global.json)).sdk.version;
        sdkSources = builtins.fromJSON (builtins.readFile ./.config/dotnet-sdk-sources.json);
        sdkRid =
          {
            x86_64-linux = "linux-x64";
            aarch64-linux = "linux-arm64";
            x86_64-darwin = "osx-x64";
            aarch64-darwin = "osx-arm64";
          }
          .${system};
        # Keep the SDK aligned with global.json while nixpkgs catches up.
        # Sources and SHA-512 hashes come from Microsoft's release metadata.
        sdkBase = pkgs.dotnetCorePackages.sdk_10_0-bin;
        sdkUnwrapped = sdkBase.unwrapped.overrideAttrs {
          version = sdkVersion;
          src = pkgs.fetchurl sdkSources.${sdkRid};
        };
        sdk = sdkBase.overrideAttrs (previous: {
          version = sdkVersion;
          src = sdkUnwrapped;
          passthru = previous.passthru // {
            unwrapped = sdkUnwrapped;
          };
        });
      in
      {
        devShells.default = pkgs.mkShell {
          packages = with pkgs; [
            # Version control
            git

            # .NET development
            sdk
            mono

            # Pre-commit and linters
            pre-commit
            nixfmt
            shellcheck

            # Python linting
            python3
            ruff
          ];

          DOTNET_CLI_TELEMETRY_OPTOUT = "1";
          DOTNET_NOLOGO = "1";
        };
      }
    );
}
