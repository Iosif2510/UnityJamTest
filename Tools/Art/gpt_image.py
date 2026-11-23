"""One image from OpenAI's image API (gpt-image-2). The key comes from OPENAI_API_KEY (user scope on Windows) and is
never printed. Every call is paid: announce the count before running.

  python gpt_image.py <out.png> <size> "<prompt>"                         new image
  python gpt_image.py <out.png> <size> "<prompt>" <image.png> <mask.png>   repaint the mask's transparent part

Size is 1024x1024, 1536x1024 or 1024x1536. For an edit, image and mask must be PNGs of that size.
"""
import base64, json, os, sys, uuid, urllib.error, urllib.request

MODEL = "gpt-image-2"


def key():
    k = os.environ.get("OPENAI_API_KEY", "").strip()
    if not k and os.name == "nt":
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as reg:
            k = winreg.QueryValueEx(reg, "OPENAI_API_KEY")[0].strip()
    if not k:
        sys.exit("OPENAI_API_KEY is not set")
    return k


def multipart(fields, files):
    boundary = uuid.uuid4().hex
    out = bytearray()
    for name, value in fields.items():
        out += f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n{value}\r\n".encode()
    for name, path in files.items():
        with open(path, "rb") as f:
            data = f.read()
        out += (f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"; "
                f"filename=\"{os.path.basename(path)}\"\r\nContent-Type: image/png\r\n\r\n").encode()
        out += data + b"\r\n"
    out += f"--{boundary}--\r\n".encode()
    return bytes(out), f"multipart/form-data; boundary={boundary}"


out, size, prompt = sys.argv[1:4]
headers = {"Authorization": f"Bearer {key()}"}
if len(sys.argv) > 5:
    body, kind = multipart({"model": MODEL, "prompt": prompt, "size": size, "quality": "medium", "n": "1"},
                           {"image": sys.argv[4], "mask": sys.argv[5]})
    url = "https://api.openai.com/v1/images/edits"
else:
    body = json.dumps({"model": MODEL, "prompt": prompt, "size": size, "quality": "medium", "n": 1}).encode()
    kind = "application/json"
    url = "https://api.openai.com/v1/images/generations"
headers["Content-Type"] = kind
try:
    with urllib.request.urlopen(urllib.request.Request(url, data=body, method="POST", headers=headers),
                                timeout=300) as res:
        data = json.loads(res.read())
except urllib.error.HTTPError as e:
    sys.exit(f"HTTP {e.code}: {e.read()[:400]!r}")
with open(out, "wb") as f:
    f.write(base64.b64decode(data["data"][0]["b64_json"]))
print(out, data.get("usage", {}))
