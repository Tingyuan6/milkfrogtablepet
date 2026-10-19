using System;
using System.Collections.Generic;

namespace Naifrog.Core
{
    public enum LayoutMode { Free, Bottom, Left, Right, Keyboard }

    public sealed class Clip
    {
        public string From, To;
        public string[] Frames;
        public int DurationMs;
    }

    public interface IClips { Clip Find(string from, string to); }

    // All time is supplied by the host. There are no delayed callbacks to race.
    public sealed class PetController
    {
        readonly IClips clips;
        readonly int[] durations;
        Clip active;
        double clipTime, frameTime;
        int direction = 1, swayIndex, swayVisited;
        string stable = "idle", desired = "idle";
        LayoutMode previousLayout;
        public LayoutMode Layout { get; private set; }
        public long Revision { get; private set; }
        public bool InTransition { get { return active != null; } }
        public bool IsSwaying { get { return active == null && IsSway(stable); } }
        public string Desired { get { return desired; } }
        public string Stable { get { return stable; } }
        public int CurrentSwayIndex { get { return swayIndex; } }
        public int OriginalFramesPresented { get; private set; }
        public double OriginalTimePlayed { get; private set; }

        public PetController(IClips clips, int[] frameDurations)
        {
            if (frameDurations == null || frameDurations.Length == 0) throw new ArgumentException("Missing sway timing");
            foreach (int duration in frameDurations) if (duration <= 0) throw new ArgumentException("Invalid frame timing");
            this.clips = clips; durations = (int[])frameDurations.Clone();
        }

        public string VisualKey
        {
            get
            {
                if (active == null) return stable;
                int index = (int)Math.Round(clipTime / active.DurationMs * (active.Frames.Length - 1));
                return active.Frames[Math.Max(0, Math.Min(active.Frames.Length - 1, index))];
            }
        }

        public string Status
        {
            get { return active == null ? stable : active.From + (direction > 0 ? " -> " : " <- ") + active.To; }
        }

        public void Tap(bool belly)
        {
            if (Layout == LayoutMode.Keyboard) Request(desired == "sit_2" ? "sit_1" : "sit_2");
            else if (Layout == LayoutMode.Free && belly) Request(desired == "sway" ? "idle" : "sway");
        }

        public void SetLayout(LayoutMode mode)
        {
            if (Layout == LayoutMode.Keyboard && mode != LayoutMode.Keyboard) return;
            Layout = mode; Request(BasePose(mode));
        }

        public void SetKeyboard(bool visible)
        {
            if (visible && Layout != LayoutMode.Keyboard)
            {
                previousLayout = Layout; Layout = LayoutMode.Keyboard; Request("sit_1");
            }
            else if (!visible && Layout == LayoutMode.Keyboard)
            {
                Layout = previousLayout; Request(BasePose(Layout));
            }
        }

        public void BeginDrag()
        {
            if (Layout != LayoutMode.Keyboard) { Layout = LayoutMode.Free; Request("idle"); }
        }

        public void Request(string pose)
        {
            desired = pose; Revision++;
            if (active != null) direction = Matches(active.To, desired) || (active.To == "idle" && !Matches(active.From, desired)) ? 1 : -1;
            else Reconcile();
        }

        public void Advance(double deltaMs)
        {
            if (deltaMs < 0 || Double.IsNaN(deltaMs) || Double.IsInfinity(deltaMs)) throw new ArgumentOutOfRangeException("deltaMs");
            int guard = 0;
            while (deltaMs > 0.00001 && guard++ < 10000)
            {
                if (active != null)
                {
                    double remaining = direction > 0 ? active.DurationMs - clipTime : clipTime;
                    double consume = Math.Min(remaining, deltaMs);
                    clipTime += direction * consume; deltaMs -= consume;
                    if (remaining - consume < 0.00001)
                    {
                        string endpoint = direction > 0 ? active.To : active.From;
                        active = null; stable = endpoint;
                        if (IsSway(endpoint) && desired == "sway") StartSway(ParseIndex(endpoint));
                        Reconcile();
                    }
                }
                else if (IsSwaying && desired == "sway")
                {
                    double remaining = durations[swayIndex] - frameTime;
                    double consume = Math.Min(remaining, deltaMs);
                    frameTime += consume; OriginalTimePlayed += consume; deltaMs -= consume;
                    if (remaining - consume < 0.00001)
                    {
                        frameTime = 0; swayVisited++;
                        if (swayVisited == durations.Length) { desired = "idle"; Revision++; Reconcile(); }
                        else { swayIndex = (swayIndex + 1) % durations.Length; stable = SwayKey(swayIndex); OriginalFramesPresented++; }
                    }
                }
                else { Reconcile(); if (active == null) break; }
            }
        }

        void Reconcile()
        {
            if (active != null || Matches(stable, desired)) return;
            string target = desired == "sway" ? SwayKey(0) : desired;
            Clip next = clips.Find(stable, target);
            if (next == null && stable != "idle") next = clips.Find(stable, "idle");
            if (next == null) throw new InvalidOperationException("Missing transition: " + stable + " -> " + target);
            active = next; clipTime = 0; direction = 1;
        }

        void StartSway(int index)
        {
            swayIndex = index; frameTime = 0; swayVisited = 0; OriginalFramesPresented = 1; OriginalTimePlayed = 0;
        }

        static bool Matches(string endpoint, string intent) { return endpoint == intent || (intent == "sway" && IsSway(endpoint)); }
        public static bool IsSway(string key) { return key.StartsWith("sway_", StringComparison.Ordinal); }
        public static string SwayKey(int index) { return "sway_" + index.ToString("00"); }
        static int ParseIndex(string key) { return Int32.Parse(key.Substring(5)); }
        public static string BasePose(LayoutMode mode)
        {
            switch (mode) { case LayoutMode.Bottom: return "bottom"; case LayoutMode.Left: return "peek_left"; case LayoutMode.Right: return "peek_right"; case LayoutMode.Keyboard: return "sit_1"; default: return "idle"; }
        }
    }
}
