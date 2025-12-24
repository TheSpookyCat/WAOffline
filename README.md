## Introduction

**Worlds Adrift: Offline** is a player-focused distribution of the current playable state of **Worlds Adrift: Reborn**, packaged with an installer and launcher for easy local play.

It is based on the Reborn project but released separately to make the game immediately accessible without MMO infrastructure or dedicated servers. While **Worlds Adrift: Reborn** continues to focus on long-term multiplayer restoration, **Offline** exists to let players jump in and play right now.
The project is maintained separately, with a clean commit history to support its narrower scope and simpler release model.

The original repository and commit used for this fork can be found at [WAReborn/WorldsAdriftReborn](https://github.com/WAReborn/WorldsAdriftReborn/commit/a2702f825c7eb471a43a05e0ba2c9c5e4a055bf8).

## Details

The current version allows you to:
- Explore the entire game world as it existed at shutdown
- Noclip from island to island (`P`)
- Create, save, and delete multiple characters
- Equip/unequip clothing and utility items

This project does **not** aim to recreate the MMO experience, shared worlds, or live services. Its purpose is to preserve the feel of Worlds Adrift in a form that is easy to install, run, and maintain for local play.

⚠ **Security notice**  
Always scan files downloaded from the internet for viruses.  
The full source code and build process are public and can be reviewed, but you are encouraged to verify anything you download.

### Installation steps

1. Go to the **Releases** tab of this repository.

2. Download the installer & launcher archive:
    - `XXYYZZ-Release.zip`

3. Extract the archive and place the launcher executable in a **dedicated folder** of your choosing.
    - The location does not matter.
    - You must keep this folder if you want to launch the game again later.

4. Run the launcher.
    - Accept any Windows SmartScreen warnings if prompted.

5. In the launcher:
    - Select a folder where the game should be installed.
    - Enter your **Steam username** (this is the username you use to log in to Steam, not your display name).

6. Click **Begin Installation**.

7. A console window will open requesting your Steam credentials.
    - This is **DepotDownloader**.
    - Your Steam password is **not stored**.
    - It is used only to download the correct version of the game.
    - You will be prompted for a Steam Guard code or asked to approve the login via the Steam mobile authenticator.
    - **Steam must be completely closed** while the download is in progress. This also applies to any other machines logged into your account such as a 2nd PC or Steam Deck.

8. Once the process completes:
    - The game files will be downloaded.
    - Required mods and backend components will be installed automatically.
    - Click **Launch** to start the game.

### Runtime notes

- When launching the game, **two console windows** will appear.
  These are backend services required for the game to function.
- **Do not** close these windows manually.
  Closing them will result in bugs & crashes.
- To exit safely:
    - Use the **Close** button in the launcher, **or**
    - Close the main game window.
      The remaining processes will shut down automatically.

## Build Instructions
First you will need the correct version of the game. Get a copy of [DepotDownloader](https://github.com/SteamRE/DepotDownloader) and run `DepotDownloader.exe -app 322780 -depot 322783 -manifest 4624240741051053915 -username <yourusername> -password <yourpassword>`
Which will download the correct game files. Copy the files over to the gameroot folder.  
⚠ Note that the most up to date steam version of the game is **Not** supported!
This is due to the game having been stripped of most of its contents in and update just before the game's shutdown.

Clone the repository including submodules using `git clone --recurse-submodules <repository>`
or (if you already cloned the repository normally) cd to your repository and run `git submodule update --init --recursive`

Next download the latest 5.x [BepInEx Release](https://github.com/BepInEx/BepInEx/releases) and unzip those files into gameroot (detailed installation instructions can be found [here](https://docs.bepinex.dev/articles/user_guide/installation/index.html)).

Also create a `steam_appid.txt` file in the gameroot which contains a single line `322780` (this is the appid and is required to start the game, else you get a steam required error).

Now open up the project sln with Visual Studio 2022 (⚠ Lower versions of Visual Studio are not supported due to this project requiring dotnet 6.0).  
⚠ Also note that at this moment ony the `Any CPU` (default) and `x64` solution platforms are supported.

Rider (JetBrains C# IDE) can open and build the solution as well. You just need to create an empty `LocalPackages` subdirectory inside the solution folder.

If your game installation is not at the default location (`C:\Program Files (x86)\Steam\steamapps\common\WorldsAdrift`) visual studio will report an error and a DevEnv.targets file should have been generated at the root of your copy of the WorldsAdriftReborn repo.
You can change the path to your game installation location, save and reopen the project sln with visual studio.

Building the [WorldsAdriftReborn](https://github.com/sp00ktober/WorldsAdriftReborn/tree/main/WorldsAdriftReborn) mod will automatically build the required [WorldsAdriftRebornCoreSdk](https://github.com/sp00ktober/WorldsAdriftReborn/tree/main/WorldsAdriftRebornCoreSdk) CoreSdkDll.dll and copies this and the built BepInEx WorldsAdriftReborn plugin to the BepInEx plugins directory of your game.
It will also give an error if you try to build WorldsAdriftReborn for an an incompatible version of the game.

Running the game locally requires you to build all projects in the solution, and subsequently starting the required servers and game:
- Start the [WorldsAdriftGameServer](https://github.com/sp00ktober/WorldsAdriftReborn/tree/main/WorldsAdriftGameServer)
- Start the [WorldsAdriftServer](https://github.com/sp00ktober/WorldsAdriftReborn/tree/main/WorldsAdriftServer).
- And then start the game.

The projects also includes launch configurations for the WorldsAdriftReborn, WorldsAdriftGameServer and WorldsAdriftServer the projects.
The launch configuration for WorldsAdriftReborn will launch the game itself (⚠ when launching worlds adrift through visual studio you have to make sure you launch the game without debugging).
You can launch everything at once by configuring the solution for Multiple Startup projects.

## Updating protobuf
At the moment the WorldsAdriftRebornCoreSdk is dependent on protobuf, in order to keep the project portable and not require and external package managers (vcpkg) we opted to include a build and publish nuget package.

This nuget package was exported by vcpkg using the `vcpkg export protobuf:x64-windows-static-md --nuget --nuget-id=WorldsAdriftReborn-protobuf-x64-windows-static-md` option of vcpkg ( see https://devblogs.microsoft.com/cppblog/vcpkg-introducing-export-command/ for more info).
And released on nuget as https://www.nuget.org/packages/WorldsAdriftReborn-protobuf-x64-windows-static-md/ .

The package can be updated by going to your locally installed vcpkg installation folder, removing any installed version of protobuf using the `vcpkg remove protobuf:x64-windows protobuf:x64-windows-static protobuf:x64-windows-static-md` command,
reinstall them using the `vcpkg install protobuf:x64-windows protobuf:x64-windows-static protobuf:x64-windows-static-md` and subsequently running the aforementioned the export command again.
This will generate a new package for you, which you can then upload to nuget, and update through the nuget package manager.

For testing purposes, you can also (instead of uploading the package to nuget) locally load an exported nuget package by placing the exported .nupkg in the LocalPackages folder of the repo,
this will make it appear in the LocalPackages package source in the nuget package manager.

Aside from https://www.nuget.org/packages/WorldsAdriftReborn-protobuf-x64-windows-static-md/ we also provide the https://www.nuget.org/packages/WorldsAdriftReborn-protobuf-x64-windows-static/ and https://www.nuget.org/packages/WorldsAdriftReborn-protobuf-x64-windows/ variants.  
⚠ Do note that if you choose to switch a variant (or to a local package) that has a different package name you will need update the proto.targets with the changed package path in order for auto compiling of the .proto files to work and be mindful of the required compilation settings changes below.

You can switch linking modes by going to the WorldsAdriftRebornCoreSdk project properties and switching various settings:
- vcpkg > Use static libraries > No / C/C++ > Code Generation > Runtime Library: MDd (default): This will dynamic link everything, which will also result in separate protobuf DLLS in the output (works with all versions of the package, however you might want to switch to https://www.nuget.org/packages/WorldsAdriftReborn-protobuf-x64-windows/ for a leaner package)
- vcpkg > Use static libraries > Yes / vcpkg > Use Use Dynamic CRT > No / C/C++ > Code Generation > Runtime Library: MTd: This will static link everything, resulting in a single output DLL. (requires https://www.nuget.org/packages/WorldsAdriftReborn-protobuf-x64-windows-static/ )
- (Current default) vcpkg > Use static libraries > Yes / vcpkg > Use Use Dynamic CRT > Yes / C/C++ > Code Generation > Runtime Library: MDd (default): This will static link everything, resulting in a single output DLL. (requires https://www.nuget.org/packages/WorldsAdriftReborn-protobuf-x64-windows-static-md/ )

# Contact us
Any support is welcome - you can [find us on Discord](https://discord.gg/pSrfna7NDx)!
