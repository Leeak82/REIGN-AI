// Shared build settings; runtime locations are resolved by Pinokio.
module.exports = {
  DOTNET_ROOT: "{{path.resolve(cwd, '.dotnet')}}",
  DOTNET_GCHeapHardLimit: "0x40000000",
  DOTNET_gcServer: "0",
  DOTNET_CLI_TELEMETRY_OPTOUT: "1",
  DOTNET_NOLOGO: "1"
}
