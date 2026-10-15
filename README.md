# KNAS 42 for Xbox (test)

Test builds of the KNAS 42 Xbox app (Xbox Series X|S and Xbox One). It opens the cable box full screen,
and the controller works as the remote:

| Button | Does |
|---|---|
| A | OK |
| B | Back (closes the guide; press again to leave) |
| LB / RB | Channel down / up |
| D-pad or left stick | Arrows |
| View | Guide |
| Y | Info |
| X | Last channel |
| LT / RT | Volume down / up |
| Right stick click | Mute |
| Menu | App settings (reload, change address, exit) |

## Installing on an Xbox in Developer Mode

1. Download the newest **KNAS-42-Xbox-....zip** from **Releases** on your PC and unzip it.
2. Make a free Microsoft developer account (individual) at partner.microsoft.com, if you don't have one.
3. On the Xbox, install the **Xbox Dev Mode** app from the Store, open it, and follow its steps (it gives you a code to enter on your PC). The Xbox restarts into Developer Mode.
4. In **Dev Home** on the Xbox, turn on **Remote Access** and set a username and password. Dev Home shows an address like `https://192.168.1.60:11443`.
5. Open that address in your PC's browser (click past the security warning), sign in, then **Add** → pick the `.msix` file from the zip. When it asks for dependencies, add every file in the zip's **Dependencies** folder. Install.
6. KNAS 42 shows up in Dev Home's app list. Open it, and it finds KNAS on your network by itself (or type your NAS's address).

Regular games don't run while the Xbox is in Developer Mode. To play games, use **Leave Developer Mode** in Dev Home; switch back with the Dev Mode app.
