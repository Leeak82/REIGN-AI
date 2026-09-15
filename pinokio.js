module.exports = {
  version: "7.0",
  title: "REIGN AI",
  description: "Local dashboard connected to the existing REIGN API on Render.",
  menu: async (kernel, info) => {
    for (const [script, text] of [['install.js', 'Installing / testing'], ['update.js', 'Updating'], ['reset.js', 'Resetting']]) {
      if (info.running(script)) return [{ default: true, text, icon: 'fa-solid fa-terminal', href: script }]
    }
    if (info.running('start.js')) {
      const local = info.local('start.js') || {}
      return [
        ...(local.url ? [{ default: true, text: 'Open dashboard', icon: 'fa-solid fa-rocket', href: local.url }] : []),
        { default: !local.url, text: 'Terminal', icon: 'fa-solid fa-terminal', href: 'start.js' },
        ...(local.api_url ? [{ text: 'API documentation', icon: 'fa-solid fa-code', href: local.api_url + '/swagger' }] : [])
      ]
    }
    const installed = info.exists('app/api/REIGN.API.dll') && info.exists('app/web/REIGN.Web.dll') && info.exists('.dotnet/dotnet' + (kernel.platform === 'win32' ? '.exe' : ''))
    return [
      ...(installed ? [{ default: true, text: 'Start', icon: 'fa-solid fa-play', href: 'start.js' }] : []),
      { default: !installed, text: installed ? 'Rebuild / test' : 'Install', icon: 'fa-solid fa-plug', href: 'install.js' },
      { text: 'Update', icon: 'fa-solid fa-download', href: 'update.js' },
      { text: 'Reset build (keep data)', icon: 'fa-solid fa-rotate-left', href: 'reset.js', confirm: 'Remove the generated application and SDK? The database and source code are preserved.' }
    ]
  }
}
