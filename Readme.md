# SVGl Plugin for Flow Launcher

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Flow Launcher Plugin](https://img.shields.io/badge/Flow%20Launcher-Plugin-blue.svg)](https://github.com/Flow-Launcher/Flow.Launcher)

A plugin for [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher) that lets you search and copy SVG icons directly to your clipboard. This plugin integrates with the [SVGL](https://svgl.app) project, providing quick access to thousands of SVG icons.

## Features

- 🔍 Search through thousands of SVG icons
- 🌓 Support for both light and dark theme icons
- 📋 One-click copy to clipboard
- 💨 Fast with built-in debounce for smooth searching
- 🗄️ Local caching for improved performance

## Installation

### Method 1: Flow Launcher Plugin Manager
1. Open Flow Launcher
2. Type `pm install SVGl Plugin`
3. Press Enter to install

### Method 2: Manual Installation
1. Download the latest release from the [releases page](https://github.com/abo3skr2019/SVGl-plugin/releases)
2. Extract the zip file
3. Move the extracted folder to `%APPDATA%\FlowLauncher\Plugins`
4. Restart Flow Launcher

## Usage

Simply activate Flow Launcher and type:

```
svg <search term>
```

For example:
- `svg github` - Search for GitHub icons
- `svg react` - Search for React icons
- `svg twitter` - Search for Twitter/X icons

When you see the icon you want, select it to copy the SVG code to your clipboard. You can choose between light and dark theme versions of each icon.

## How It Works

This plugin connects to the SVGL API to fetch SVG icons based on your search query. The icons are cached locally to improve performance on subsequent searches. The plugin implements debouncing to prevent excessive API requests while you're still typing.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## Credits

- [SVGL Project](https://svgl.app) - For providing the API and amazing collection of SVG icons
- [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher) - The launcher platform

## License

This project is licensed under the MIT License - see the LICENSE file for details.

---

If you find this plugin useful, a star on the repository is always appreciated! ⭐