# ARUCONANOPIANO (ULTRAARUCO APPLICATION)
## Operation and Installation Guide with Application and Repository
## Table of Contents
- [Installing the Application](#setup)
- [Installing the Repository in UnityProject](#installunity)
- [Running the Test Example in UnityProject](#testlaunch)
- [Connecting to the Server from the Application](#serverconnect)
- [Assigning Aruco Markers to Keys, Hands, and Piano](#arucoassign)
- [Checking Server Access on the Local Network](#checkaccess)
- [Example of Operation](#workexample)
- [Documentation for the Bridge Assets/UnityHTTPServer.cs](#bridgedoc)

Please, refer to the following work:

<a id="setup"></a>
### Installing the Application
The application is installed in the form of an .apk file. 
To compile the .apk please follow the instruction:
- proceed into flutter_app folder
- run:
  ```bash
  flutter build apk --release --no-tree-shake-icons
  ```
  to obtain compiled app.
The application is in the release build. The application name is camera_demo. After opening the .apk and tapping on the agreement, the application should appear on the desktop.

<a id="installunity"></a>
### Installing the Repository in UnityProject
Project version:
```
m_EditorVersion: 6000.5.0f1
m_EditorVersionWithRevision: 6000.5.0f1 (88b47c5e7076)
```
Installation via Git:
Since the repository is private, make sure you have configured a token on your local computer and in your GitHub account for access:
[Official instructions](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens).
Once everything is ready, you can clone:
```bash
git clone https://github.com/Alexander-ha/ArucoNanoPiano-2026-06-19_03-18-33.git
```
Next, launch Unity Hub on your PC.
1. "Open" -> "Add Unity Project from disk"
2. Specify the folder with the repository
3. Double-click on the project to open it
4. Wait for the Unity project import to complete.

<a id="testlaunch"></a>
### Running the Test Example in UnityProject
> 📌 **Note:** These processes are not required if you already know what to do: you can skip directly to the [Documentation for the Bridge Assets/UnityHTTPServer.cs](#bridgedoc),
> to use HTTP access functions, as well as to [Connecting to the Server from the Application](#serverconnect) and [Example of Operation](#workexample).

If the installation was successful, you will see the following in the SampleScene scene window:
![image 1, scene](arcuopianoinstr/inspectorinstr0.png)

Here you can see that in the Canvas tree there is a Panel, followed by several Text objects.
In the example we will use, everything is simple: 5 text fields are placed on the panel.

On the right side in the inspector you can see the following:
![image 1, inspector](arcuopianoinstr/inspectorinstr1.png)

This is the main script Assets/UnityHTTPServer ([link to it in the repository](Assets/UnityHTTPServer.cs)).
It is added as a Component to the MainCamera.
Here in order:
1. Fields for text (on the scene values from the HTTP request will be parsed into them). The text field objects themselves are dragged from the left side of the scene (those under Canvas, if they haven't been dragged yet).
```cs
    public Text statusText;
    public Text leftHandText;
    public Text rightHandText;
    public Text pianoText;
    public Text extraKeysText;
```
2. Port. Be sure to specify a free port.

Once everything is configured, press ▶, the server will start and listen on the port you specified (default is 8081).

<a id="serverconnect"></a>
### Connecting to the Server from the Application
Now that the server is running, run the following in your PC's cmd:
```bash
ipconfig

>
Wireless LAN adapter Wi-Fi:

   Connection-specific DNS Suffix . . . . . . : Dlink
   Link-local IPv6 Address . . . . . . . . : fe80::d55e:1f9e:3655:ebc4%9
   IPv4 Address. . . . . . . . . . . . . . : 192.168.0.138
   Subnet Mask . . . . . . . . . . . . . . : 255.255.255.0
   Default Gateway . . . . . . . . . . . . : 192.168.0.1

Ethernet adapter outline-tap0:

   Media State . . . . . . . . . . . . . . : Media disconnected
   Connection-specific DNS Suffix . . . . . :

Ethernet adapter Bluetooth Network Connection:

   Media State . . . . . . . . . . . . . . : Media disconnected
   Connection-specific DNS Suffix . . . . . :
```

From what you get, you will need the IPv4 address.

Go to the camera_demo.apk application, **which you downloaded to your phone**.

![image 3. menu](arcuopianoinstr/Screenshot_2026-06-21-23-10-56-646_com.example.flora_nano_aruco.jpg)

In the application, tap Connect to Local Server.

![image 4. connect](arcuopianoinstr/Screenshot_2026-06-21-22-40-37-466_com.example.flora_nano_aruco.jpg)

Enter your PC server's IPv4 address on the Unity server in the **local Wi-Fi network**, as well as the port you specified when starting the script.

If everything is done correctly, you will see a green inscription with a checkmark indicating that you are connected.

> [!WARNING]
> Typical problems that can lead to unsuccessful connection:
> 1. **Your PC running the Unity server and your phone are NOT on the same Wi-Fi network**. Solution: connect to the same Wi-Fi.
> 2. **Your PC has a Firewall running.** Solution: ubuntu ufw is disabled via sudo ufw disable, windows: netsh advfirewall set allprofiles state off.
> 3. **You disabled the firewall via the terminal, but the network is identified as public instead of private** Solution: on Windows you can go to Control Panel\System and Security\Windows Defender Firewall and disable all firewalls.
> 4. **Your Android version for some reason does not support current certificates** Solution: message me @altergan1 (Telegram, dimetranow@gmail.com GMAIL), we'll figure something out.

<a id="arucoassign"></a>
### Assigning Aruco Markers to Keys, Hands, and Piano
Once you connect, the key selection screen will open immediately. If not: tap Enter ArUco ID in the main menu.

![image 5. keys](arcuopianoinstr/Screenshot_2026-06-22-00-52-01-479_com.example.flora_nano_aruco.jpg)

Here you can:
1. specify the Aruco marker on the left hand (Left Hand ID)
2. specify the Aruco marker on the right hand (Right Hand ID)
3. specify the Aruco marker on the piano (Piano ID)
4. via add key + add an unlimited number of markers on keys, each assigned an ID that denotes your specific Aruco marker.

<a id="checkaccess"></a>
### Checking Server Access on the Local Network

A good option if there is no connection is to check whether access to your Unity PC server is available in general:
to do this, you can enter the IP and port directly into the browser: http://192.168.0.138:8081/
And you will see if you have access.
If not - return to the Warning section in [Connecting to the Server from the Application](#serverconnect).

<a id="workexample"></a>
### Example of Operation
If everything is configured correctly, you can tap Open Camera in your application and grant access to it.
After that, you can touch markers (example in the photo):

![touch1](arcuopianoinstr/Screenshot_2026-06-21-23-39-36-447_com.example.flora_nano_aruco.jpg)

![touch2](arcuopianoinstr/Screenshot_2026-06-22-00-52-54-088_com.example.flora_nano_aruco.jpg)

On the server you will see the following:
![server](arcuopianoinstr/instructionarucopiano.png)

> [!NOTE]
> 1. **Markers are sent to the server only in the format of x,y coordinates of the bounding box corners. If the z coordinate is needed, use the trick of calculating depth on the server itself (the code will have to be written separately).**
> 2. **If the marker is not visible, null is sent to the server**
> 3. **The extraKeys tuple has arbitrary size depending on how many keys you assigned in the Android application**

Example transmitted json:

```json
1. We see all markers:
{
  "left_hand": {
    "id": 1,
    "corners": [{"x": 100, "y": 200}, {"x": 150, "y": 205}, {"x": 155, "y": 260}, {"x": 105, "y": 255}]
  },
  "right_hand": {
    "id": 2,
    "corners": [{"x": 300, "y": 200}, ...]
  },
  "piano": {
    "id": 3,
    "corners": [{"x": 500, "y": 400}, ...]
  },
  "extra_keys": {
    "key_4": {
      "id": 4,
      "corners": [{"x": 200, "y": 300}, ...]
    }
  }
}
2. We do not see left_hand and key_4 (but the rest are visible):
{
  "left_hand": null,
  "right_hand": {
    "id": 2,
    "corners": [{"x": 300, "y": 200}, ...]
  },
  "piano": {
    "id": 3,
    "corners": [{"x": 500, "y": 400}, ...]
  },
  "extra_keys": {
    "key_4": null
  }
}
```

<a id="bridgedoc"></a>
### Documentation for the Bridge Assets/UnityHTTPServer.cs
The main script that allows configuring HTTP access is located in assets, it is UnityHTTPServer.cs. It provides standard access to GET requests, inherits from MonoBehaviour.

## 📋 Public Methods

### `StartHTTPServer()`
Starts the HTTP server on the specified port.

```csharp
public void StartHTTPServer()
```

**Example:**
```csharp
UnityHTTPServer server = GetComponent<UnityHTTPServer>();
server.StartHTTPServer();
```

---

### `ProcessReceivedData(string jsonData)`
Processes the incoming JSON, updates the user interface (via the passed text fields).

```csharp
public void ProcessReceivedData(string jsonData)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `jsonData` | `string` | JSON string with gesture data |

**Example:**
```csharp
server.ProcessReceivedData("{\"left_hand\":{\"corners\":[{\"x\":100,\"y\":200}]}}");
```

---

### `UpdateUI(RootObject data)`
Updates all UI elements based on the received data.

```csharp
public void UpdateUI(RootObject data)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `data` | `RootObject` | Deserialized object with data |

---

### `CalculateCenter(List<Corner> corners)`
Calculates the center of an object from a list of corner points. (may be useful for positioning.)

```csharp
public Vector2 CalculateCenter(List<Corner> corners)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `corners` | `List<Corner>` | List of points (x, y) |

**Returns:** `Vector2` — the center of the object.

**Example:**
```csharp
Vector2 center = CalculateCenter(handData.corners);
Debug.Log($"Center: ({center.x}, {center.y})");
```

---

### `GetLocalIPAddress()`
Gets the local IP address of the computer (auto IP configuration).

```csharp
public string GetLocalIPAddress()
```

**Returns:** `string` — IP address (e.g., `192.168.1.100`).

**Example:**
```csharp
string ip = GetLocalIPAddress();
statusText.text = $"Server: http://{ip}:8080/";
```

---

## 🔒 Private Methods

| Method | Description |
|--------|-------------|
| `HandleRequests()` | coroutine for asynchronous request processing |
| `ProcessRequest(IAsyncResult result)` | handles incoming HTTP request |
| `Update()` | update every frame |

---

## 📦 Nested Classes

### `Corner`
point.

```csharp
public class Corner
{
    public float x;
    public float y;
}
```

### `HandData`
hand.

```csharp
public class HandData
{
    public int id;
    public List<Corner> corners;
}
```

### `PianoData`
piano.

```csharp
public class PianoData
{
    public int id;
    public List<Corner> corners;
}
```

### `KeyData`
key.

```csharp
public class KeyData
{
    public int id;
    public List<Corner> corners;
}
```

### `ExtraKeys`
container for keys.

```csharp
public class ExtraKeys
{
    public KeyData key_1;
    public KeyData key_2;
    // ... up to key_10
}
```

### `RootObject`
JSON root.

```csharp
public class RootObject
{
    public HandData left_hand;
    public HandData right_hand;
    public PianoData piano;
    public ExtraKeys extra_keys;
}
```

---

## 🎯 Example Usage

```csharp
// get the server
UnityHTTPServer server = GetComponent<UnityHTTPServer>();

// start it with current parameters
server.StartHTTPServer();

// getter for IP (we can get it)
string ip = server.GetLocalIPAddress();
Debug.Log($"Server started on {ip}:8080");

// manual deserialization
string json = "{\"left_hand\":{\"corners\":[{\"x\":100,\"y\":200}]}}";
server.ProcessReceivedData(json);

// barycenter
List<Corner> corners = new List<Corner>();
corners.Add(new Corner { x = 0, y = 0 });
corners.Add(new Corner { x = 100, y = 100 });
Vector2 center = server.CalculateCenter(corners);
Debug.Log($"Center: {center}");
```

---

## ⚡ Unity Events
default events
| Event | When called |
|-------|-------------|
| `Start()` | scene started - server started |
| `Update()` | Every frame — processes received data |
| `OnDestroy()` | stop - destroying the server object from memory|

---

# 🎹 `Piano` Branch — 3D Visualization of Piano and Hands

This branch extends the project with a full 3D scene: markers from the phone (via the same
HTTP bridge `UnityHTTPServer.cs`) control the **piano model**, **key presses**, and
**two hands with multi-finger chord pressing**.

## Dependencies
- **Unity 6000.5.0f1**, URP.
- **Ultraleap Tracking 7.3.0** — installs automatically from the **OpenUPM** registry
  (`scopedRegistries` already specified in `Packages/manifest.json`), Unity will download it on the first
  project opening. Only the **Ghost Hands** hand mesh rig is used (without Leap hardware).
  > ⚠️ If on Unity 6.5 you get the error `CS0619: GetInstanceID is obsolete` from
  > `com.ultraleap.tracking/Core/Editor/Scripts/EditorUtils.cs` — comment out the two lines with
  > `GetInstanceID()` (lines ~33 and ~56) or replace with `GetEntityID()`. This is a known
  > incompatibility of the package with Unity 6.5.
- **Newtonsoft Json** (already in dependencies).

## File Structure
- `Assets/ArucoPiano3D/` — runtime scripts and editor tools:
  - `ArucoKeyboardController.cs` — main controller (piano position, key states, hands).
  - `PianoKey3D.cs` — key (press animation + edge/center contact points).
  - `GhostHandRig.cs` — hand driver: 5 fingers with CCD-IK, chords, finger spreading without crossing.
  - `Hand3D.cs`, `HandDriverBase.cs` — simple cube hand and common base class.
  - `Editor/` — builders and inspectors (see menu `Tools ▸ ArucoPiano`).
- `Assets/Piano/` — piano model (`Piano.fbx`).
- `Assets/MinimalArucoTest.cs` — minimal 2D overlay for smoke-testing the bridge.
- `Assets/Scenes/PianoModel.unity` — main 3D scene.

## Quick Start
1. Open the project in Unity 6000.5.0f1, wait for import (Ultraleap will download from OpenUPM).
2. Menu **`Tools ▸ ArucoPiano ▸ Build 3D Piano Model Scene`** — creates `Assets/Scenes/PianoModel.unity`:
   piano model, one top camera, 10 interactive keys (2 groups of 4 white + 1 black).
3. Menu **`Tools ▸ ArucoPiano ▸ Add Ghost Hands To Scene`** — adds two Ghost hands and connects them.
4. Menu **`Tools ▸ ArucoPiano ▸ Recolor Hands (Skin)`** — colors the hands skin color.
5. Start the server (▶ Play). In the phone app (see instructions above) connect to the server and
   assign markers: **Piano ID**, **10 keys** (`key_1..key_10`, left to right), **Left/Right Hand ID**.
6. Open the camera, point at the markers.

## Logic
- **Piano** moves within a square zone on the floor (X,Y from the frame + depth from marker size) and
  **rotates only according to the real marker** (yaw from marker corners).
- **Key** is pressed (tilt + orange highlight) only if a finger **actually reaches it**.
- **Hands**: the nearest hand moves (≤5 cm from marker position) to the pressed keys; fingers reach for
  their press points via CCD-IK; holds a **chord** of multiple fingers; fingers are spread while
  maintaining order (without crossing). Thumb — edge of the white key, the rest — center;
  black keys — edge (index–pinky). At rest, the index–pinky are slightly bent.

## Tuning (Inspector)
- `ArucoKeyboardController`: `Zone Size`, `Hand Reach`, `Hand Px To Local`, `Height Min/Max`, `Yaw Sign`.
- `GhostHandRig` (on `GhostHand_L`/`GhostHand_R`): `Max Pull` (≤5 cm pulling), `Max Reach`,
  `Contact Threshold`, `Idle Curl Angle`, `Move Speed`.
- `PianoKey3D`: `Edge/Center Fraction` (press location along the key), `Press Angle`.

## Useful Menus (`Tools ▸ ArucoPiano`)
- `Build 3D Piano Model Scene` / `Add Ghost Hands To Scene` / `Recolor Hands (Skin)`.
- `Convert Piano Materials to URP` — if the piano model renders pink (Standard shader in URP).
- `Inspect Piano Model` / `Inspect Ghost Hand` — log hierarchy (for debugging bindings).
- `Build Demo Scene` / `Setup Minimal Visual Test` — old cube scenes and 2D overlay for smoke-testing.
