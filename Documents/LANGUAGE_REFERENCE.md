# Ferrolite Language Reference — Complete

**September 2026**

Ferrolite is a scripting language for **desktop automation and game macros**.
It reads like English: you *say* what you want the computer to do — move the
mouse, press keys, push a gamepad, watch the screen — and it does it.

Open a new script (Ctrl+N), type something, press **Run** (Ctrl+Shift+G).

---

## 1. Your first script

```
print("hello, ferrolite")     # writes to the terminal below
```

```
let name = "recruit"
print("welcome, " + name)
```

A **recoil-control macro** — no magic, just plain steps:

```
let pull = 7

print("Hold BOTH mouse buttons while Caps Lock is on to pull down.")

while true {
    # ']' increases pull strength, '[' decreases it
    if is_key_down("]") {
        pull += 1
        print("Pull strength: " + str(pull))
        wait(0.2)
    }

    if is_key_down("[") {
        pull -= 1
        if pull < 0 {
            pull = 0
        }
        print("Pull strength: " + str(pull))
        wait(0.2)
    }

    # armed: Caps Lock on + left and right mouse held
    if is_key_toggled("caps") and is_mouse_down("left") and is_mouse_down("right") {
        move_relative(0, pull)
        wait(0.01)
    } else {
        wait(0.01)
    }
}
```

> Ctrl+Shift+G runs the script. Ctrl+Shift+Backspace stops it. Pause and the
> Speed box (0.25x–4x) work while it runs.

---

## 2. The language

### Comments
`# anything after a hash is ignored`

### Values
| Kind | Example | Notes |
|---|---|---|
| number | `42`, `3.5`, `-1` | all numbers are decimals |
| text | `"hi"` | quotes around text |
| true/false | `true`, `false` | booleans print lowercase |
| nothing | `null` | empty value |

### Variables
`let name = value` creates one. Reassign with `name = value`.
Operators: `+ - * / %`, `+= -= *= /=`, comparisons `== != < <= > >=`,
logic `and or not`.

### Lists and maps
```
let loot = ["knife", "armor", "potion"]   # ordered list
loot[0]                                   # "knife"

let settings = { speed: 4, color: "red" } # map of key -> value
settings["color"]                         # "red"
settings["speed"] = 5
```

### Control flow
```
if score > 100 { print("high") } else { print("low") }

while health > 0 {
    health -= 10
}

for item in loot {           # item = each value in turn
    print(item)
}

for i in range(1, 6) {       # 1,2,3,4,5
    print(i)
}
```

### Your own functions
```
func double(x) {
    return x * 2
}
print(double(21))            # 42

# arrow form for one-liners
map([1, 2, 3], func(x) => x * 2)     # [2, 4, 6]
```

### Errors
Errors print with the line number — click it to jump. Catch your own:

```
try {
    risky_thing()
} catch problem {
    print("failed: " + str(problem))
}
```

---

## 3. Complete function reference

Everything below is built in. Where two names do the same thing
(e.g. `move` and `mouse_move`), **either one works**.
Legend: `[ ]` = optional. `{...}` = a map, `[...]` = a list.

### Output & conversion (6)
| Function | What it does |
|---|---|
| `print(value, ...)` | Write values to the terminal |
| `log(...)` / `debug(...)` | Same as print (debug prefixes the line) |
| `str(x)` | Convert anything to text |
| `num(s)` / `bool(x)` / `type(x)` / `typeof(x)` | Convert / inspect |

### Math (35)
| Function | What it does |
|---|---|
| `abs(x)` | Absolute value |
| `round(x, [digits])` `floor(x)` `ceil(x)` | Rounding |
| `sqrt(x)` `pow(base, exp)` | Roots and powers |
| `min(a, b, ...)` `max(a, b, ...)` | Smallest / largest |
| `clamp(x, lo, hi)` | Bound a value |
| `sin cos tan asin acos atan atan2(y,x)` | Trigonometry |
| `sinh cosh tanh` | Hyperbolic |
| `log(x)` — *math log* | Natural logarithm |
| `random()` | 0..1 random |
| `random_int(lo, hi)` `random_float(lo, hi)` | Ranged random |
| `random_choice(list)` | Pick one |
| `gcd(a, b)` `lcm(a, b)` | Common divisors |
| `factorial(n)` `is_prime(n)` | Number theory |
| `sum(list)` `avg(list)` `median(list)` `stdev(list)` | Stats |
| `lerp(a, b, t)` `map_range(x, inLo, inHi, outLo, outHi)` | Transitions |
| `pi` | Constant ≈ 3.14159 |

### Text / strings (48)
| Function | What it does |
|---|---|
| `upper(s)` `lower(s)` `to_upper` / `to_lower` | Case |
| `title_case(s)` `camel_case(s)` `snake_case(s)` | Name styles |
| `trim(s)` `trim_start(s)` `trim_end(s)` `ltrim` `rtrim` | Whitespace |
| `len(s)` | Character count |
| `contains_str(s, part)` | Substring test |
| `starts_with(s, part)` `ends_with(s, part)` | Prefix / suffix |
| `index_of(s, part)` | First position or -1 |
| `char_at(s, i)` `substring(s, from, [to])` | Pieces |
| `char_code(c)` `char_from(n)` | Character ↔ code |
| `replace(s, from, to)` | Substitute |
| `repeat(s, n)` | Repeat |
| `pad_left(s, n, [ch])` `pad_right(s, n, [ch])` | Pad |
| `split(s, sep)` `split_lines(s)` `join(list, sep)` | Split / join |
| `count_str(s, part)` | Occurrences |
| `reverse_str(s)` | Reverse |
| `is_number(s)` `is_alpha(s)` `is_digit(s)` `is_empty(s)` | Tests |
| `format(pattern, ...)` | `"{0} score"`-style templates |
| `mask(s, [ch])` | Hide characters |
| `escape_html(s)` `unescape_html(s)` | HTML safety |
| `insert(s, i, part)` | Insert into text |

### Lists (34)
| Function | What it does |
|---|---|
| `len(list)` | Size |
| `push(list, v)` `pop(list)` | Stack |
| `append(list, v)` `remove(list, v)` | Add / remove value |
| `insert(list, i, v)` `remove_at(list, i)` | Positional edit |
| `sort(list)` `reverse(list)` | Arrange |
| `unique(list)` `flatten(list)` `flatten_deep(list)` | Clean up |
| `slice(list, from, to)` `take(list, n)` `drop(list, n)` | Pieces |
| `chunk(list, n)` `rotate(list, n)` | Reshape |
| `contains(list, v)` `index_of(list, v)` | Search |
| `find(list, fn)` `find_index(list, fn)` | Predicate search |
| `map(list, fn)` `filter(list, fn)` `reduce(list, fn)` | Transform |
| `foreach(list, fn)` | For-each side effects |
| `zip_lists(a, b)` | Pair up two lists |
| `group_by(list, fn)` | Bucket |
| `shuffle(list)` `sample(list, n)` | Randomness |
| `range(n)` `range(lo, hi)` | Number lists |

### Maps (14)
| Function | What it does |
|---|---|
| `keys(m)` `values(m)` `entries(m)` *(to_pairs)* | Views |
| `len(m)` `has_key(m, k)` | Inspect |
| `set(m, k, v)` `delete_key(m, k)` | Edit |
| `merge(a, b)` `invert(m)` | Combine / flip |
| `map_map(m, fn)` *(map_values)* `map_filter(m, fn)` | Transform |
| `pick(m, keys)` | Subset |
| `set_union(a, b)` `set_intersection(a, b)` `set_difference(a, b)` | Set math |

### JSON, encoding, hashes (12)
| Function | What it does |
|---|---|
| `json_parse(text)` `json_stringify(v)` | JSON ↔ values |
| `base64_encode(s)` `base64_decode(s)` | Base64 |
| `hex_encode(s)` `hex_decode(s)` | Hex |
| `url_encode(s)` `decode_url(s)` | URL escaping |
| `md5(s)` `sha256(s)` `uuid()` | Hashes / ids |

### Time (14)
| Function | What it does |
|---|---|
| `wait(s)` *(sleep, delay)* | Pause — stoppable |
| `now()` `now_ms()` `timestamp()` | Current time |
| `today()` `current_hour()` `current_minute()` | Clock pieces |
| `current_time_string()` | Human-readable |
| `format_date(t, "%Y-%m-%d %H:%M")` | Pretty print |
| `date_diff(a, b)` | Difference |
| `timer(label)` | Stopwatch |
| `wait_until(fn, timeout)` | Wait for a condition |

### Mouse (16)
| Function | What it does |
|---|---|
| `move(x, y, [seconds])` *(mouse_move)* | Glide cursor |
| `move_relative(dx, dy)` *(mouse_move_relative)* | Nudge — raw hardware deltas |
| `get_mouse_pos()` *(mouse_position)* | `{x, y}` |
| `click([x, y, [button]])` | Click |
| `double_click([x, y])` `right_click([x, y])` | Variants |
| `mouse_down(btn)` `mouse_up(btn)` | Hold / release |
| `mouse_hold(btn, seconds)` | Timed hold |
| `drag(x1, y1, x2, y2, [seconds])` | Press-drag-release |
| `scroll(amount)` | Wheel |
| `is_mouse_down("left"/"right"/"middle")` | Button state |

### Keyboard (12)
| Function | What it does |
|---|---|
| `type_text(s)` | Type like a person |
| `key_press(k)` *(press_key)* | Tap |
| `key_down(k)` `key_up(k)` `key_hold(k, seconds)` | Hold |
| `hotkey(["ctrl", "c"])` *(key_combo)* | Combo |
| `is_key_down(k)` | Held right now? |
| `is_key_toggled(k)` | Caps/Num/Scroll latch on? |

Key names: letters, digits, punctuation (`"["`, `"]"`, `";"`, …),
`enter tab space backspace delete esc caps`, `shift ctrl alt`
(`lshift`/`rshift`…), `f1`…`f12`, `up down left right`,
`lwin`, `printscreen`, `pageup pagedown home end insert`.
Punctuation and letters resolve through the active keyboard layout.

### Windows (10)
| Function | What it does |
|---|---|
| `find_window(title)` `focus_window(title)` | Find / foreground |
| `get_active_window()` | Foreground window title |
| `list_windows()` | All top-level windows |
| `minimize_window(t)` `maximize_window(t)` `close_window(t)` | Control |
| `move_window(t, x, y, w, h)` | Move / resize |
| `window_exists(t)` | Existence test |

### Screen & pixels (19)
| Function | What it does |
|---|---|
| `get_pixel_color(x, y)` *(get_pixel)* | `{r, g, b}` |
| `get_pixel_r/g/b(x, y)` | One channel |
| `get_pixel_block(x, y, w, h)` | Region of pixels |
| `find_color({r,g,b})` | Where is this color? |
| `find_color_region(color, x, y, w, h)` | Scoped search |
| `wait_for_color(color, timeout)` `wait_for_color_gone(color, timeout)` | Block until |
| `color_in_region(color, x, y, w, h)` | Test region |
| `color_distance(a, b)` | Perceptual distance |
| `screen_size()` | `{width, height}` |
| `screenshot()` | Capture to image object |
| `save_screenshot(path)` | Capture to PNG |
| `compare_images(a, b)` | Similarity |
| `find_image_on_screen(img, [confidence])` | OpenCV template match |

### Controller — physical input (9)
| Function | What it does |
|---|---|
| `controller_connected(id)` | Pad present? |
| `controller_button_down(id, btn)` | Button held? |
| `controller_stick_x(id, stick)` `controller_stick_y(id, stick)` | Axis −1..1 |
| `controller_trigger_value(id, trig)` | Trigger 0..1 |
| `controller_driver()` | Active chain, e.g. `xinput+vigem+hidhide` |
| `controller_passthrough(on, [physical_id])` | Arm the physical→virtual chain. **Non-blocking** — the script keeps running and its output is merged onto the virtual pad. The chain is torn down when the script ends (or on Stop / `controller_passthrough(false)`). |
| `controller_passthrough_status()` | `{active, hid_hidden, virtual_pad, note, physical_paths, vjoy_paths, hidden_paths, hidden_at_arm}` |
| `controller_hide_report()` | Diagnose the hiding layer: `{chain, elevated, passthrough_active, hidhide_available, hidhide_mode, hidhide_last_failure, physical_pads, xinput_devnodes, vjoy_device, hidden_now, hidden_at_arm, not_hidden, verdict}` |

Button names for `controller_button_down`: `a b x y lb rb l3 r3 start back`,
D-pad `up down left right` (+ `up_left` etc.), triggers
`left_trigger right_trigger`, sticks `left_stick right_stick`.
Works with XInput pads (Xbox, SCUF over USB) **and** DualSense/PS5 pads over
Bluetooth via the built-in HID fallback.

### Controller — script output & virtual pad (12)
| Function | What it does |
|---|---|
| `controller_press(id, btn)` `controller_release(id, btn)` | Tap a button |
| `controller_click(id, btn, [times])` | Click helper |
| `controller_hold(id, btn, seconds)` | Timed hold |
| `controller_stick(id, stick, x, y)` | Deflect (−1..1) |
| `controller_trigger(id, trig, value)` | Pull (0..1) |
| `controller_dpad(id, dir)` | D-pad |
| `virtual_controller_count()` | Virtual pads active |
| `virtual_controller_connected(id)` | Virtual pad present? |
| `virtual_controller_set_state(id, state)` | Full state write |
| `virtual_controller_set_enabled(id, on)` | Enable / disable |
| `virtual_controller_button_down(id, btn)` | Button on the pad the game sees |
| `virtual_controller_trigger_value(id, trig)` | Trigger 0..1 the game sees |
| `virtual_controller_stick_x(id, stick)` `virtual_controller_stick_y(id, stick)` | Axis −1..1 the game sees |
| `vjoy_enabled()` | vJoy present? |

While `controller_passthrough(true)` is armed, script output is **merged**
with your real hands on the virtual pad the game sees. The
`virtual_controller_*` readback functions report that merged, game-facing
state — use them to detect the pad the game actually receives.

`controller_passthrough(true)` **returns immediately** — the rest of the
script runs normally while the chain mirrors your physical pad. Typical
macro:

```ferrolite
controller_passthrough(true)      # arm; script keeps running
wait(0.5)
controller_hold(0, "a", 300)      # merged on top of your real input
wait(2)
                                  # script ends → chain is torn down
```

The chain also tears down on `controller_passthrough(false)`, on Stop
(Ctrl+Shift+Backspace), or on error. It never outlives the script that armed
it — keep the script alive (e.g. with `wait(...)` or a loop, or an overlay
with `gui_*`) for as long as you want the chain up. While it is armed, the
status bar shows a **CONTROLLER ARMED** chip (orange = physical pad hidden,
red = the pad is still visible to games).

### Is my controller actually hidden?

One command answers it. Run this and read the `verdict` line:

```ferrolite
controller_passthrough(true)
wait(0.5)
print(controller_hide_report())
```

| Report says | Meaning |
|---|---|
| `OK: every physical interface is hidden…` | Game sees only the virtual pad |
| `LEAK: n of m physical interfaces are not hidden` | The `not_hidden` list names what other apps can still see |
| `XINPUT LEAK: your physical controller presents as an Xbox/XInput pad…` | The pad is in Xbox/XInput mode (`XnaComposite`). Games read it through XInput, which the HID-class cloak cannot mask — the physical pad will stay listed next to the virtual one. Put the controller in PS/DualSense (HID) mode for single-pad emulation. `physical_xinput_pads` lists the devnodes |
| `XInput-ONLY physical pad…` | Your pad exposes no HID interface, only XInput — HidHide filters the HID class, so it cannot be hidden while the pad stays in that mode |
| `no physical controller detected…` | Pad asleep/disconnected — wake it and re-arm |

**Virtual pad type** — chosen in *Settings → Virtual controller* (or left at
the default Xbox 360):

- **Xbox 360** — the game sees a genuine "Xbox 360 Controller". Works
everywhere; use this unless a game needs PlayStation prompts.
- **DualShock 4** — the game sees a "Wireless Controller" (PS4 pad), with
A/B/X/Y mapped to Cross/Circle/Square/Triangle and Guide to the PS button.
For games that show PlayStation button glyphs. Change takes effect on the
next script run.

### Recorder (5)
| Function | What it does |
|---|---|
| `play_recording(name)` | Replay a saved recording |
| `begin_session()` `close_session()` | Explicit record scopes |
| Record via UI | Ctrl+Shift+R; stop dialog saves/inserts/decouples |

Recordings store hardware-level deltas and exact delays; replay adds no
smoothing.

### Files & CSV (13)
| Function | What it does |
|---|---|
| `file_write(path, text)` `file_append(path, text)` | Write |
| `file_append_line(path, text)` | Append with newline |
| `file_read(path)` *(read_file)* | Read all text |
| `file_size(path)` `file_exists(path)` `file_delete(path)` | Inspect |
| `list_dir(path)` | Folder listing |
| `make_dir(path)` `dir_exists(path)` | Folders |
| `list_files(path, [pattern])` | Recursive listing |
| `csv_read(path)` `csv_write(path, rows)` | CSV tables |

### System (19)
| Function | What it does |
|---|---|
| `clipboard_get()` `clipboard_set(text)` | Clipboard |
| `notify(title, msg)` *(show_notification)* | Toast |
| `beep(freq, ms)` | Console beep |
| `open_url(url)` `open_file(path)` *(launch)* | Open things |
| `desktop_path()` `documents_path()` `downloads_path()` `temp_path()` | Folders |
| `env_get(name)` | Environment |
| `cpu_usage()` `memory_info()` | Machine stats |
| `run_process(path, args)` | Start and capture |
| `process_exists(name)` `kill_process(name)` `list_processes()` | Process control |

### Web — HTTP (5)
| Function | What it does |
|---|---|
| `http_get(url)` | GET → `{status, body}` |
| `http_post(url, data)` `http_put(url, data)` `http_delete(url)` | Mutating verbs |
| `http_download(url, path)` | Save to file |

### Database — SQLite (4)
| Function | What it does |
|---|---|
| `db_open(path)` | Open (creates if missing) |
| `db_query(db, sql)` | SELECT → list of maps |
| `db_execute(db, sql)` | INSERT/UPDATE/DDL |
| `db_close(db)` | Close |

### GUI overlays (13)
| Function | What it does |
|---|---|
| `gui_create_overlay(title, w, h)` | Create overlay window |
| `gui_add_label(id, text)` `gui_add_button(id, text)` `gui_add_toggle(id, text)` | Elements |
| `gui_set_element_position(id, x, y)` `gui_set_element_text(id, text)` | Element props |
| `gui_set_visible(id, on)` `gui_set_position(id, x, y)` `gui_set_always_on_top(id, on)` | Window props |
| `gui_button_clicked(id)` `gui_get_toggle(id)` | Poll input |
| `gui_remove_element(id)` `gui_close_overlay(id)` | Teardown |

### Debugging (6)
| Function | What it does |
|---|---|
| `assert(cond)` | Fail loudly when false |
| `debug_break()` | Break into the debugger |
| `debug_locals()` | Print current variables |
| `profile_start()` `profile_end()` | Time a section |

---

## 4. Debugging your script

Click **Debug** (or F5) instead of Run. Then:
- Click the left gutter to drop a **red breakpoint** (F9 toggles).
- **F5** Continue · **F10** Step Over · **F11** Step Into · **Shift+F11** Step Out.
- The side panel shows every variable in scope plus the call stack.
- Errors with "at line N" are clickable and jump to the problem.

## 5. Keyboard shortcuts

| Shortcut | Action |
|---|---|
| Ctrl+N / Ctrl+O / Ctrl+S | New / Open / Save |
| Ctrl+W · Ctrl+Tab | Close tab · next tab |
| F2 | Rename the tab |
| Ctrl+Shift+G | Run |
| Ctrl+Shift+R | Record input |
| Ctrl+Shift+Backspace | Stop everything |
| F5 / F9 / F10 / F11 | Debug: continue / breakpoint / step over / step into |

---

*Naming rule of thumb: the long technical name and the short friendly name
both work (`reverse_str` / `reverse_text`, `map_map` / `map_values`).
If a name feels like a tongue-twister, try the friendlier one.*
