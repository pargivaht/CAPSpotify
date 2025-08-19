# CAPSpotify

CAPSpotify remaps the Caps Lock key to pause/resume Spotify playback (only on the desktop app, windows only).
You can find the config and other options by right clicking the tray app. Its reccomended to add it to startup, so you don't need to start it every time you start your pc.
The config file (and the exe if you added to startup) can be found at %appdata%\.pargivaht\CAPSpotify\

## Features

- Caps Lock -> play/pause
- Caps Lock + Shift -> next
- Caps Lock + Ctrl -> previous
- Caps Lock + Alt -> like current song (only with API)

All that is free and does not need Spotify premium.
The only feature that needs a bit of work to set up is the liking current song, if you dont feel like tinkering for 2 min you can just disable it from the config.

## How to get the API working?

To get the API part working you need to create an app at https://developer.spotify.com/dashboard. Make sure to set the "Redirect URI" to http://localhost:5555/callback and check the Web API checkbox. After creating the app copy the Client ID and Client secret to the config.


© 2025 pargivaht. 
