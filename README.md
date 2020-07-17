# Lidgren.Network ![](https://api.travis-ci.org/RevoluPowered/lidgren-network-gen3.svg?branch=master)
Lidgren.Network is a networking library for .NET, which uses a single UDP socket to deliver a simple API for connecting a client to a server, reading and sending messages. It supports **.NET Framework 4.5**+ and **.NET Standard 2.0**+.

This has been updated for use with Unity3D, feel free to send PRs for other bugs fixes.
To use this in Unity3D just enable the experimental .NET framework.
you can do this in Edit -> Project Settings -> Player -> Other Settings -> Api Compatibility Level -> .NET 4.6

Platforms supported:
- Linux
- MacOS
- Windows

Platforms/Toolchains which need testing:
- Android
- iOS
- Xamarin

Tested in:
- Mono (alpha and beta)
- .NET 4.6
- Unity 2017.1 -> 2018.1.
- .NET Core 3

Future Roadmap:
- Update to latest .NET 4.6
- Investigate officially supporting .NET Core.
- Improve test suite so that tests are run on all platforms we support, for each release.
