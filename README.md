# vlcshim by IsThisThisandFr

This project mirrors VLC playback state into Windows SMTC using VLC's Lua HTTP interface.

Please read [README_BEFORE_CODE_OF_CONDUCT.md](./README_BEFORE_CODE_OF_CONDUCT.md) before opening issues or contributing.
Please also read [CONTRIBUTING.md](./CONTRIBUTING.md) if you want to send issues or pull requests.

## Setup VLC

1. Open VLC.
2. Go to `Tools > Preferences`.
3. Switch to `All` settings.
4. Open `Main Interface > Main interfaces`.
5. Enable `Web`.
6. Open `Main Interface > Lua`.
7. Set a password for `Lua HTTP`.
8. Restart VLC.

## Run the shim

1. Build and launch with `dnet-cbr.bat`.
2. If you changed the VLC Lua HTTP password from the default `ineedair`, run the shim with `--password your_password_here`.
3. If VLC is using a different web port, run with `--port your_port_here` or `--ports 8080,your_port_here`.
4. You can also set `VLC_HTTP_PASSWORD` instead of passing `--password`.
5. The shim will keep retrying until VLC's HTTP interface responds.
6. Use the tray icon menu to open `Config...` if you want to change the shown player identity or toggle the startup warning toast.

## Troubleshooting

1. Double-check that VLC Web is enabled and VLC was restarted after changing the Lua HTTP password.
2. Confirm the password and port match your VLC configuration.
3. If your Windows build behaves differently, check the SMTC docs for your target build.
4. Try a simple password and a known port such as `8080` to rule out configuration mistakes.

## FAQ

- "What version has this been tested on?"
  Windows build `26100` and `26200.8079`.

- "What does this do?"
  It mirrors VLC playback state into Windows SMTC and forwards SMTC button presses back to VLC.

- "Does this modify VLC or Windows files?"
  No. It talks to VLC through its HTTP interface and uses the Windows media control APIs from a separate app.

- "Is this real-time?"
  It is polling-based, so responsiveness depends on your system and how quickly VLC responds over HTTP.

- "What does `dnet-cbr.bat` do?"
  It runs `dotnet clean`, `dotnet build`, and then starts the generated executable.

- "Can I fork this project?"
  Yes. Please keep attribution and be clear about what your fork changes.

- "What is SMTC?"
  See the Microsoft Learn docs:
  <https://learn.microsoft.com/en-us/uwp/api/windows.media.systemmediatransportcontrols?view=winrt-26100>

## Project expectations

This project assumes that you:

1. Know how to use Visual Studio Code or Visual Studio.
2. Understand basic object-oriented programming concepts.
3. Can follow written setup instructions.
4. Have VLC installed and know how to open its settings.
5. Are comfortable editing command-line arguments or environment variables if needed.

If you do not meet those assumptions, setup will be harder, but the project is still open to improvements and documentation fixes.
