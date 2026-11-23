// Run with: unity command --project-path <project> script eval --file Tools/Sfx/level_sfx.cs
// after adding or regenerating sounds (synth_sfx.py, eleven_sfx.py), so every sound sits at its loudness target.
// Re-levels every sound in Resources/Sfx to a loudness target by kind and writes it back as 16-bit WAV
// (the original file is replaced). Targets are the RMS of the audible part, in dBFS; peaks are kept under -1 dB.
using UnityEngine;
using System.Collections.Generic;
using System.IO;
var targets = new Dictionary<string, float>
{
    // moments that should land
    ["StampThud"] = -18f, ["Splash"] = -20f, ["SlimePop"] = -20f, ["Fanfare"] = -20f, ["Flop"] = -21f,
    ["BrewDone"] = -21f, ["DoorBell"] = -22f, ["FoundBell"] = -21f, ["Serve"] = -22f,
    // paper and furniture: present but soft
    ["PageTurn"] = -25f, ["CardDeal"] = -26f, ["Paper"] = -25f, ["Card"] = -24f, ["Drawer"] = -24f,
    // small interface sounds (call sites scale these further)
    ["Click"] = -22f, ["Tick"] = -22f, ["Hover"] = -26f, ["Select"] = -21f, ["Blip"] = -20f,
    ["StepChime"] = -22f, ["Heartbeat"] = -18f,
    // workshop, counter and loft extras
    ["Gulp"] = -21f, ["Whoosh"] = -27f, ["FireFlare"] = -23f, ["Purr"] = -24f,
    ["Step1"] = -24f, ["Step2"] = -24f, ["Step3"] = -24f, ["Step4"] = -24f,
};
var sb = new System.Text.StringBuilder();
string dir = Application.dataPath + "/Resources/Sfx/";
foreach (var clip in Resources.LoadAll<AudioClip>("Sfx"))
{
    if (!targets.TryGetValue(clip.name, out float target)) continue;
    clip.LoadAudioData();
    var data = new float[clip.samples * clip.channels];
    clip.GetData(data, 0);
    float peak = 0f; foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
    int win = Mathf.Max(1, clip.frequency * clip.channels / 50);
    var rms = new List<float>();
    for (int i = 0; i + win <= data.Length; i += win) { double s = 0; for (int j = i; j < i + win; j++) s += data[j] * data[j]; rms.Add(Mathf.Sqrt((float)(s / win))); }
    float loud = 0f; foreach (var r in rms) loud = Mathf.Max(loud, r);
    double acc = 0; int n = 0; foreach (var r in rms) if (r > loud * 0.0316f) { acc += r * r; n++; }
    float active = Mathf.Sqrt((float)(acc / Mathf.Max(1, n)));
    float gain = Mathf.Pow(10f, (target - 20f * Mathf.Log10(Mathf.Max(active, 1e-6f))) / 20f);
    gain = Mathf.Min(gain, 0.89f / Mathf.Max(peak, 1e-6f));
    // write 16-bit PCM WAV
    string path = dir + clip.name + ".wav";
    using (var w = new BinaryWriter(File.Create(path)))
    {
        int bytes = data.Length * 2;
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)clip.channels);
        w.Write(clip.frequency); w.Write(clip.frequency * clip.channels * 2); w.Write((short)(clip.channels * 2)); w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
        foreach (var v in data) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * gain * 32767f), -32768, 32767));
    }
    string mp3 = dir + clip.name + ".mp3";
    if (File.Exists(mp3)) { File.Delete(mp3); if (File.Exists(mp3 + ".meta")) File.Delete(mp3 + ".meta"); }
    sb.Append($"{clip.name}: {20 * Mathf.Log10(Mathf.Max(active, 1e-6f)):F1} -> {20 * Mathf.Log10(Mathf.Max(active * gain, 1e-6f)):F1} dB (x{gain:F2})\n");
}
return sb.ToString();
