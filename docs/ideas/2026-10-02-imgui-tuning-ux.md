# IMGUI tuning window UX (Unity 6 / Valheim / BepInEx)

Goal: make `FogTuningWindow` (~20 floats, 13 anchor rows) pleasant. Sources are listed at the end. Unity API facts are from the IMGUI docs and common practice; none of this is verified in-game.

## 1. Big, precise controls
- **Scale the whole window, not the skin.** Set `GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s,s,1))` at the start of `OnGUI`, with `s = Screen.height / 1080f` times a user-config factor (clamped 1..2.5). Reset the matrix afterwards. Mouse events are transformed automatically. `GUIUtility.ScaleAroundPivot` is the same thing with a pivot. Keep the window `Rect` in unscaled units.
- **Custom slider styles.** Copy `GUI.skin.horizontalSlider` and `horizontalSliderThumb` into new `GUIStyle`s. Set the slider's `fixedHeight = 22`. Set the thumb's `fixedWidth = 18` and `fixedHeight = 26`. Replace `normal.background` (and `hover`, `active`) with 1x1 or 4x4 `Texture2D`s (`SetPixel`, `Apply`, `hideFlags = HideFlags.HideAndDontSave`). Use a dark track and a bright thumb. Pass the styles to `GUILayout.HorizontalSlider(v, min, max, slider, thumb, GUILayout.Height(26))`.
- **Row layout:** `[label 140px][slider flexible][-][value field 60px][+][reset]`. The value is always visible and typeable. A slider alone never makes the value obvious.
- **Typed field:** keep a per-row string buffer keyed by control name. Draw with `GUI.SetNextControlName(id); GUILayout.TextField(...)`. Parse with `float.TryParse(..., NumberStyles.Float, CultureInfo.InvariantCulture)` only on Enter or focus loss. Use `Event.current.isKey && keyCode == Return`, or compare `GUI.GetNameOfFocusedControl()` with the previous frame. Never parse every keystroke.
- **Step buttons** `-` / `+` change the value by `step`. Make them repeat-buttons (`GUILayout.RepeatButton`) if holding should scrub.
- **Modifiers:** read `Event.current.shift` / `.control`. Shift means step/10, Ctrl means step*10. For the dragged slider, apply the modifier to the delta from the value at mouse-down (store it on `EventType.MouseDown`). Otherwise the thumb jumps to the cursor.
- **Mouse wheel:** in the row, `if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition)) { v -= Mathf.Sign(e.delta.y) * step; e.Use(); }`. Get `rect` from `GUILayoutUtility.GetLastRect()`. Wheel events inside a `ScrollView` conflict, so require the cursor to be over the slider only, or hold Alt.
- **Keyboard:** if `GUIUtility.keyboardControl == controlId` (get the id with `GUIUtility.GetControlID(FocusType.Keyboard)`), arrow keys step the value. `GUI.FocusControl(name)` focuses a named control. Tab key navigation comes free for named controls.

## 2. Feedback
- Use **apply-on-change with debounce** (already 0.2s) for visuals, because you want to see the fog while dragging. Use **apply-on-release** (`MouseUp`, `e.rawType` because the control may already have used the event) for expensive things such as re-spawning emitters.
- Show state: a small status label ("Applied 12:03:41" vs "Pending..."), and tint the changed-from-saved value (`GUI.color = Color.yellow` when `!Mathf.Approximately(v, saved)`). Prefix changed labels with a dot. Keep a `saved` snapshot taken on Save or Reset.
- Per-row reset button (`↺`) returns to the default. Disable it with `GUI.enabled = changed`.

## 3. Presets
- Presets are a `Dictionary<string, float[]>` or a struct snapshot of the tier look. Store them as ConfigEntries (one string per preset, `key=value;...`) or a small JSON file next to the cfg.
- **Copy tier → tier** is a button that clones the look struct. **Undo** is one level: push a snapshot before Reset, Load or Copy, and pop it on "Undo".
- **Preview mode:** a toggle "follow tab". When `_tab` changes, call the dev command that gives or sets the tier (`DevCommands`). Debounce so rapid tab clicks do not spam the game.

## 4. Layout
- Tabs: `GUILayout.Toolbar`. Foldouts: `show = GUILayout.Toggle(show, (show ? "▼ " : "▶ ") + title, "Label")`.
- One `GUILayout.BeginScrollView` around the body. Use a fixed header and footer (Save, Reset, Undo, status) outside it.
- Anchor grid: `BeginHorizontal` with two `BeginVertical` columns, 13 rows as toggle + two offset fields. Make anchor offset fields compact (50px).
- `GUI.Window` + `GUI.DragWindow(new Rect(0,0,10000,24))`. Add a resize grip bottom-right (drag with `EventType.MouseDrag`). Persist `rect.x/y/w/h` and the scale factor in config on window close.
- Cursor: you already block input. Also set `Cursor.lockState = CursorLockMode.None; Cursor.visible = true` every frame while open, because Valheim's camera resets them. Your approach of toggling `GameCamera.m_mouseCapture` is the right hook. Call `GUI.UnfocusWindow()` or `GUIUtility.keyboardControl = 0` on close so text fields do not swallow game keys.

## 5. Alternatives
- **BepInEx ConfigurationManager** (F1): entries with `AcceptableValueRange<float>` become sliders with a value box and a reset-to-default button. It supports live edit, search, descriptions as tooltips, and custom drawers via the `ConfigurationManagerAttributes.CustomDrawer` tag. Per-tier fog values exposed as config entries (e.g. `Fog.Tier1/Density`) would be editable live if your code subscribes to `SettingChanged`. It is a good free fallback and a good player-facing UI. It would **not replace** our window: no tabs per tier, no anchor grid, no presets or copy tier, no preview mode, and its sliders are also quite thin. Idea: keep both. Expose the main values as entries (players tune them with F1) and keep the dev window for anchors and presets.
- **Jötunn `GUIManager`** gives Valheim-styled uGUI panels, sliders and buttons. It is nicer-looking and mouse-friendly but is more work (prefab-style construction, layout groups, extra dependency). Not worth it for a debug tool.
- **RuntimeUnityEditor** inspects and edits fields and components of live objects (useful to find the actual shader or material properties). It is a complement, not a tuner.

## 6. Recommendation
Keep IMGUI, add window scaling and a single reusable `Row` helper, and use it for all 20 floats. Then add per-row reset and changed-tint, shift/ctrl modifiers, and one-level undo. Add presets later. Expose 5-6 key values to ConfigurationManager as a bonus.

```csharp
static bool Row(string label, ref float v, float min, float max, float step, float def, float saved)
{
    float old = v; var e = Event.current;
    bool changed = !Mathf.Approximately(v, saved);
    GUILayout.BeginHorizontal();
    var c = GUI.color; if (changed) GUI.color = new Color(1f, .9f, .4f);
    GUILayout.Label(label, GUILayout.Width(140)); GUI.color = c;
    float mod = e.shift ? .1f : e.control ? 10f : 1f;               // fine / coarse
    v = GUILayout.HorizontalSlider(v, min, max, SliderStyle, ThumbStyle, GUILayout.Height(26), GUILayout.MinWidth(120));
    Rect r = GUILayoutUtility.GetLastRect();
    if (e.type == EventType.ScrollWheel && r.Contains(e.mousePosition)) { v -= Mathf.Sign(e.delta.y) * step * mod; e.Use(); }
    if (GUILayout.Button("-", GUILayout.Width(26))) v -= step * mod;
    string id = "row_" + label; GUI.SetNextControlName(id);
    string txt = _buf.TryGetValue(id, out var b) && GUI.GetNameOfFocusedControl() == id ? b : v.ToString("0.###", CultureInfo.InvariantCulture);
    string n = GUILayout.TextField(txt, GUILayout.Width(60)); _buf[id] = n;
    if (n != txt && float.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) && e.keyCode == KeyCode.Return) v = p;
    if (GUILayout.Button("+", GUILayout.Width(26))) v += step * mod;
    GUI.enabled = !Mathf.Approximately(v, def);
    if (GUILayout.Button("↺", GUILayout.Width(26))) v = def;
    GUI.enabled = true; GUILayout.EndHorizontal();
    v = Mathf.Clamp(Snap(v, step), min, max);                        // Snap: round to step/10 grid
    return !Mathf.Approximately(v, old);
}
```
(`_buf` is a `Dictionary<string,string>`. The Enter parse needs a small tweak: apply `p` when Return is pressed while focused. Treat the sketch as a starting point, not tested code.)

## Sources
- Unity IMGUI manual and scripting API: https://docs.unity3d.com/Manual/GUIScriptingGuide.html, `GUIStyle`, `GUI.matrix`, `GUILayout.HorizontalSlider`, `GUIUtility.keyboardControl`, `Event`.
- BepInEx.ConfigurationManager: https://github.com/BepInEx/BepInEx.ConfigurationManager (README fetched 2026-10-02: F1 window, `AcceptableValueRange` sliders, live editing, custom drawers).
- Jötunn: https://github.com/Valheim-Modding/Jotunn (`GUIManager`).
- RuntimeUnityEditor: https://github.com/ManlyMarco/RuntimeUnityEditor
