"""Generates instrumental background music through sunoapi.org. The token comes from the SUNO_API_KEY environment
variable (user scope on Windows) and is never printed.

  python suno_music.py credits
  python suno_music.py <out_dir> [track ...]

Each call returns a couple of takes; they are downloaded as <track>_<n>.mp3 for listening before one is chosen.
"""
import json, os, sys, time, urllib.request

BASES = ["https://api.sunoapi.org", "https://apibox.erweima.ai"]
TRACKS = {
    "Title": ("Potion Shop Morning",
              "cozy fantasy village shop, warm acoustic guitar, pizzicato strings, light flute, soft hand percussion, "
              "gentle and cheerful, medium-slow tempo, loopable game background music, instrumental"),
    "Loft": ("Notes in the Attic",
             "quiet attic study by candlelight, music box and soft felt piano, warm low strings, calm and thoughtful, "
             "slow tempo, sparse, cozy fantasy game background music for reading, instrumental"),
    "Hurry": ("Before the Sun Sets",
              "cozy fantasy potion shop, playful but urgent, pizzicato strings and ticking woodblock, bright flute runs, "
              "faster tempo, rising tension without being scary, short loopable game music, instrumental"),
}


def key():
    k = os.environ.get("SUNO_API_KEY", "").strip()
    if not k and os.name == "nt":
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as reg:
            k = winreg.QueryValueEx(reg, "SUNO_API_KEY")[0].strip()
    if not k:
        sys.exit("SUNO_API_KEY is not set")
    return k


def call(base, path, token, body=None):
    req = urllib.request.Request(base + path, data=json.dumps(body).encode() if body is not None else None,
                                 method="POST" if body is not None else "GET",
                                 headers={"Authorization": f"Bearer {token}", "Content-Type": "application/json",
                                          "User-Agent": "potion-shop-tools"})
    with urllib.request.urlopen(req, timeout=60) as res:
        return json.loads(res.read())


def base_for(token):
    for base in BASES:
        try:
            r = call(base, "/api/v1/generate/credit", token)
            if r.get("code") == 200:
                return base, r.get("data")
        except Exception as e:  # try the other documented host
            last = e
    sys.exit(f"credit check failed on both hosts: {last}")


def generate(base, token, name, out_dir):
    title, style = TRACKS[name]
    body = {"customMode": True, "instrumental": True, "model": "V6", "title": title, "style": style,
            "negativeTags": "vocals, singing, choir, heavy drums, distorted guitar, edm",
            "callBackUrl": "https://example.com/no-callback"}
    r = call(base, "/api/v1/generate", token, body)
    if r.get("code") != 200:
        print(f"{name}: generate failed {r.get('code')} {r.get('msg')}")
        return
    task = r["data"]["taskId"]
    for _ in range(90):
        time.sleep(10)
        info = call(base, f"/api/v1/generate/record-info?taskId={task}", token).get("data") or {}
        status = info.get("status")
        if status == "SUCCESS":
            for i, track in enumerate((info.get("response") or {}).get("sunoData") or []):
                url = track.get("audio_url") or track.get("stream_audio_url")
                path = os.path.join(out_dir, f"{name}_{i + 1}.mp3")
                urllib.request.urlretrieve(url, path)
                print(f"{name}_{i + 1}: {track.get('duration', 0):.0f}s")
            return
        if status and status.endswith(("FAILED", "ERROR", "EXCEPTION")):
            print(f"{name}: {status} {info.get('errorMessage')}")
            return
    print(f"{name}: timed out")


if __name__ == "__main__":
    token = key()
    base, credits = base_for(token)
    print(f"credits: {credits}")
    if sys.argv[1:2] == ["credits"]:
        sys.exit(0)
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    for name in sys.argv[2:] or list(TRACKS):
        generate(base, token, name, out_dir)
    print(f"credits left: {base_for(token)[1]}")
