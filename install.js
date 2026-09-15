const env = require('./launcher-env')
const dotnet = '"{{path.resolve(cwd, platform === "win32" ? ".dotnet/dotnet.exe" : ".dotnet/dotnet")}}"'
module.exports = {
  run: [{
    method: "shell.run",
    params: {
      message: "node -e \"require('fs').mkdirSync('.installer', {recursive:true}); require('fs').mkdirSync('data', {recursive:true})\""
    }
  }, {
    when: "{{platform !== 'win32'}}",
    method: "shell.run",
    params: { conda: { skip: true }, message: [
      "curl -fL https://dot.net/v1/dotnet-install.sh -o .installer/dotnet-install.sh",
      "bash .installer/dotnet-install.sh --version 10.0.401 --install-dir .dotnet --no-path"
    ] }
  }, {
    when: "{{platform === 'win32'}}",
    method: "shell.run",
    params: { conda: { skip: true }, message: [
      "curl -fL https://dot.net/v1/dotnet-install.ps1 -o .installer/dotnet-install.ps1",
      "powershell -NoProfile -ExecutionPolicy Bypass -File .installer/dotnet-install.ps1 -Version 10.0.401 -InstallDir .dotnet -NoPath"
    ] }
  }, {
    method: "shell.run",
    params: { conda: { skip: true }, env, message: [
      `${dotnet} build REIGN.slnx -c Release --nologo -m:1 -p:UseSharedCompilation=false`,
      `${dotnet} test REIGN.slnx -c Release --no-build --nologo -m:1 -- RunConfiguration.DotNetHostPath=${dotnet}`,
      `${dotnet} publish REIGN.API/REIGN.API.csproj -c Release --no-build --no-restore -o app/api`,
      `${dotnet} publish REIGN.Web/REIGN.Web.csproj -c Release --no-build --no-restore -o app/web`
    ] }
  }].map(step => {
    if (step.method === 'shell.run') {
      step.params.conda = { skip: true }
      step.params.env = { ...step.params.env, LD_LIBRARY_PATH: '' }
      const messages = Array.isArray(step.params.message) ? step.params.message : [step.params.message]
      step.params.message = messages.map(command => command + ' || echo REIGN_INSTALL_FAILED')
      step.params.on = [{ event: '/^REIGN_INSTALL_FAILED\\r?$/m', break: true }]
    }
    return step
  })
}
