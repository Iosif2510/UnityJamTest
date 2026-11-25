"""Generates sound effects with the ElevenLabs sound-generation API. The key comes from the ELEVENLABS_API_KEY
environment variable (user scope on Windows) and is never printed.

  python eleven_sfx.py <out_dir> [name ...]

Each entry fixes its duration (cost is per second), so the budget is known before calling. Outputs MP3 files named
after the game's sound (Resources/Sfx/<name>), to be reviewed before they replace the synthesized ones.
"""
import json, os, sys, time, urllib.request

SOUNDS = {
    # name: (seconds, prompt)
    "PageTurn": (0.5, "a single sheet of old paper being turned over quickly on a wooden desk, soft and close, no room echo"),
    "CardDeal": (0.5, "a small card of thick paper flicked down onto a wooden table, one short soft tap"),
    "Paper": (0.5, "short rustle of a parchment label being picked up, soft and close"),
    "Card": (0.6, "a single stiff parchment card slid across and set down on a wooden counter, a light papery swish ending in a soft pat, close and clean, no room echo"),
    "StampThud": (0.6, "a heavy wooden-handled wax-seal stamp slammed down once onto paper on a thick wooden desk, a deep, punchy, satisfying 'thunk' with a tiny paper crunch, close, no echo, no second hit"),
    "SlimePop": (0.5, "a cute wet slime bubble popping, cartoon, short and squishy"),
    "Drawer": (0.8, "a small wooden drawer sliding open smoothly and stopping with a soft knock"),
    "Splash": (0.8, "a small object dropped into a bubbling cauldron of thick potion, a short gloopy splash"),
    "Flop": (0.8, "a sad cartoon deflating trombone-like 'bwomp', soft and short, comedic failure"),
    "Serve": (0.7, "a glass potion bottle set down on a wooden counter with a light clink, cheerful"),
    "FoundBell": (1.0, "a small brass hand bell rung twice quickly, bright and clear, short ring out"),
    "BrewDone": (1.2, "a magical potion finishing brewing, a soft sparkling chime with a gentle bubbling pop, cozy fantasy"),
    "DoorBell": (1.5, "a small brass shop door bell jingling as the door opens, cozy little shop"),
    "Fanfare": (1.5, "a short cheerful magical success jingle, rising bells and sparkles, cozy fantasy game, no voice"),
}


def key():
    k = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not k and os.name == "nt":
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as reg:
            k = winreg.QueryValueEx(reg, "ELEVENLABS_API_KEY")[0].strip()
    if not k:
        sys.exit("ELEVENLABS_API_KEY is not set")
    return k


def generate(name, out_dir, api_key):
    seconds, prompt = SOUNDS[name]
    body = json.dumps({"text": prompt, "duration_seconds": seconds, "prompt_influence": 0.6}).encode()
    req = urllib.request.Request("https://api.elevenlabs.io/v1/sound-generation?output_format=mp3_44100_128",
                                 data=body, method="POST",
                                 headers={"xi-api-key": api_key, "Content-Type": "application/json"})
    started = time.time()
    with urllib.request.urlopen(req, timeout=120) as res:
        data = res.read()
    path = os.path.join(out_dir, f"{name}.mp3")
    with open(path, "wb") as f:
        f.write(data)
    print(f"{name}: {seconds}s, ~{seconds * 40:.0f} credits, {len(data) // 1024} KB, {time.time() - started:.1f}s")


if __name__ == "__main__":
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    names = sys.argv[2:] or list(SOUNDS)
    total = sum(SOUNDS[n][0] for n in names)
    print(f"{len(names)} sounds, {total:.1f}s, ~{total * 40:.0f} credits")
    k = key()
    for n in names:
        try:
            generate(n, out_dir, k)
        except urllib.error.HTTPError as e:
            print(f"{n}: HTTP {e.code} {e.read()[:200]!r}")
