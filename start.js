const env = require("./launcher-env")
const dotnet = `"{{path.resolve(cwd, platform === "win32" ? ".dotnet/dotnet.exe" : ".dotnet/dotnet")}}"`
const runtime = {
  ...env,
  ASPNETCORE_ENVIRONMENT: "Development",
  DOTNET_ENVIRONMENT: "Development",
  Logging__LogLevel__Default: "Warning",
  "Logging__LogLevel__Microsoft.Hosting.Lifetime": "Information",
  PORT: "",
  WEBSITES_PORT: ""
}

module.exports = {
  daemon: true,
  run: [{
    method: "shell.run",
    params: {
      path: "app/web",
      conda: { skip: true },
      env: {
        ...runtime,
        ASPNETCORE_URLS: "http://127.0.0.1:{{port}}",
        REIGN_API_BASE_URL: "https://reign-ai-3.onrender.com/",
        PINOKIO: "1"
      },
      message: `${dotnet} REIGN.Web.dll`,
      on: [{ event: "/(http:\\/\\/[0-9.:]+)/", done: true }]
    }
  }, {
    method: "local.set",
    params: { url: "{{input.event[1]}}" }
  }]
}
