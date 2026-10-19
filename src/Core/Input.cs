using System;

namespace Naifrog.Core
{
    public sealed class Gesture
    {
        double x, y, threshold;
        public bool Down { get; private set; }
        public bool Dragging { get; private set; }
        public void Begin(double px, double py, double distance) { x = px; y = py; threshold = distance; Down = true; Dragging = false; }
        public bool Move(double px, double py)
        {
            if (!Down) return false;
            bool before = Dragging;
            if ((px-x)*(px-x)+(py-y)*(py-y) >= threshold*threshold) Dragging = true;
            return Dragging && !before;
        }
        public bool End() { bool click = Down && !Dragging; Down = false; Dragging = false; return click; }
        public void Cancel() { Down = false; Dragging = false; }
    }

    public struct Box
    {
        public double Left, Top, Right, Bottom;
        public Box(double left, double top, double right, double bottom) { Left=left; Top=top; Right=right; Bottom=bottom; }
    }

    public sealed class DockResolver
    {
        LayoutMode candidate = LayoutMode.Free, current = LayoutMode.Free;
        double enteredAt;
        public LayoutMode Evaluate(Box body, Box work, double scale, double now)
        {
            LayoutMode next = Pick(body,work,scale);
            if (next != candidate) { candidate = next; enteredAt = now; }
            if (now - enteredAt >= 120) current = candidate;
            return current;
        }
        LayoutMode Pick(Box body,Box work,double scale)
        {
            double enter=24*scale,leave=48*scale;
            LayoutMode next=LayoutMode.Free;
            if (body.Left <= work.Left + (current == LayoutMode.Left ? leave : enter)) next = LayoutMode.Left;
            else if (body.Right >= work.Right - (current == LayoutMode.Right ? leave : enter)) next = LayoutMode.Right;
            else if (body.Bottom >= work.Bottom - (current == LayoutMode.Bottom ? leave : enter)) next = LayoutMode.Bottom;
            return next;
        }
        public LayoutMode Drop(Box body,Box work,double scale){current=candidate=Pick(body,work,scale);return current;}
        public void Reset() { candidate = current = LayoutMode.Free; enteredAt = 0; }
    }

    public static class HitRegions
    {
        public static bool Belly(double x, double y)
        {
            double dx=(x-160)/65, dy=(y-166)/70;
            return dx*dx+dy*dy <= 1 && y >= 102 && y <= 235;
        }
    }
}
