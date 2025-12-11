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
      in
      {
        devShells.default = pkgs.mkShell {
          packages = with pkgs; [
            # Version control
            git

            # .NET development
            dotnet-sdk_10
            dotnet-runtime_10
            mono

            # Pre-commit and linters
            pre-commit
            nixfmt-rfc-style
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
