using System;
using Dalamud.Plugin;
using ImGuiNET;
using Dalamud.Configuration;
using Num = System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.Command;
using System.Linq;
using System.Collections.Generic;

namespace SlideCast
{
    public class SlideCast : IDalamudPlugin
    {
        public string Name => "Slide Cast";
        private DalamudPluginInterface pluginInterface;
        public Config Configuration;

        public bool enabled = true;
        public bool config = false;
        public bool debug = false;
        public bool uiCastBar = false;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetBaseUIObjDelegate();
        private GetBaseUIObjDelegate getBaseUIObj;
        [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
        private delegate IntPtr GetUI2ObjByNameDelegate(IntPtr getBaseUIObj, string UIName, int index);
        private GetUI2ObjByNameDelegate getUI2ObjByName;

        public IntPtr scan1;
        public IntPtr scan2;

        public IntPtr chatLog;
        public IntPtr castBar;

        public int cbX = 0;
        public int cbY = 0;
        public float cbScale = 0f;
        public int cbCastTime = 0;
        public float cbCastPer = 0f;
        public int slideTime = 50;
        public Num.Vector4 slideCol;
        public int wait = 1000;
        public bool check = true;
        public float cbCastLast = 0;
        public int cbCastSameCount = 0;
        public bool cbCasting = false;
        public List<byte> cbSpell = new List<byte>();
        public string cbSpellname = "";

        public Colour col_s = new Colour(0.04f, 0.8f, 1f, 1f);
        public Colour col1_s = new Colour(0.04f, 0.4f, 1f, 1f);
        public Colour col2_s = new Colour(0.8f, 1f, 0f, 1f);
        public Num.Vector4 colp1_s = new Num.Vector4(0.04f, 0.4f, 1f, 1f);
        public Num.Vector4 colp2_s = new Num.Vector4(0.8f, 1f, 0f, 1f);
        public int diffH_s = 0;
        public int diffS_s = 0;
        public int diffB_s = 0;

        //public Lumina.Excel.ExcelSheet<Lumina.Excel.Generated> actionSheet;


        public void Initialize(DalamudPluginInterface pluginInterface)
        {

            diffH_s = col2_s.Hue - col1_s.Hue;
            diffS_s = col2_s.Saturation - col1_s.Saturation;
            diffB_s = col2_s.Brightness - col1_s.Brightness;

            this.pluginInterface = pluginInterface;
            Configuration = pluginInterface.GetPluginConfig() as Config ?? new Config();


            scan1 = pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 41 b8 01 00 00 00 48 8d 15 ?? ?? ?? ?? 48 8b 48 20 e8 ?? ?? ?? ?? 48 8b cf");
            scan2 = pluginInterface.TargetModuleScanner.ScanText("e8 ?? ?? ?? ?? 48 8b cf 48 89 87 ?? ?? 00 00 e8 ?? ?? ?? ?? 41 b8 01 00 00 00");

            getBaseUIObj = Marshal.GetDelegateForFunctionPointer<GetBaseUIObjDelegate>(scan1);
            getUI2ObjByName = Marshal.GetDelegateForFunctionPointer<GetUI2ObjByNameDelegate>(scan2);

            if (getUI2ObjByName(Marshal.ReadIntPtr(getBaseUIObj(), 0x20), "_CastBar", 1) != IntPtr.Zero)
            { castBar = getUI2ObjByName(Marshal.ReadIntPtr(getBaseUIObj(), 0x20), "_CastBar", 1); }
            else { castBar = IntPtr.Zero; }

            this.pluginInterface.UiBuilder.OnBuildUi += DrawWindow;
            this.pluginInterface.UiBuilder.OnOpenConfigUi += ConfigWindow;
            this.pluginInterface.CommandManager.AddHandler("/slc", new CommandInfo(Command)
            {
                HelpMessage = "Open SlideCast config menu"
            });

            //actionSheet = pluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>();

            try
            { enabled = Configuration.Enabled; }
            catch (Exception)
            {
                PluginLog.LogError("Failed to set Enabled");
                enabled = false;
            }

            try
            {
                if (Configuration.SlideTime.HasValue)
                {
                    slideTime = Configuration.SlideTime.Value;
                }
                else
                {
                    slideTime = 50;
                }
            }
            catch (Exception)
            {
                PluginLog.LogError("Failed to set SlideTime");
                slideTime = 50;
            }

            try
            {
                if (Configuration.SlideCol != null)
                {
                    slideCol = Configuration.SlideCol;
                }
                else
                {
                    slideCol = new Num.Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                }
            }
            catch (Exception)
            {
                PluginLog.LogError("Failed to set SlideCol");
                slideCol = new Num.Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            }

            pluginInterface.ClientState.OnLogout += (s, e) => enabled = false;
            pluginInterface.ClientState.OnLogin += (s, e) => enabled = Configuration.Enabled;
        }

        public void Command(string command, string arguments)
        {
            config = true;
        }

        public void Dispose()
        {
            this.pluginInterface.UiBuilder.OnBuildUi -= DrawWindow;
            this.pluginInterface.UiBuilder.OnOpenConfigUi -= ConfigWindow;
            this.pluginInterface.CommandManager.RemoveHandler("/slc");
        }

        private void ConfigWindow(object Sender, EventArgs args)
        {
            config = true;
        }

        private void DrawWindow()
        {
            if (check)
            {
                IntPtr tempCastBar = getUI2ObjByName(Marshal.ReadIntPtr(getBaseUIObj(), 0x20), "_CastBar", 1);
                if (tempCastBar != IntPtr.Zero)
                {
                    wait = 1000;
                    if (config)
                    {
                        ImGui.SetNextWindowSize(new Num.Vector2(300, 500), ImGuiCond.FirstUseEver);
                        ImGui.Begin("SlideCast Config", ref config);
                        ImGui.Checkbox("Enable", ref enabled);
                        ImGui.InputInt("Time (cs)", ref slideTime);
                        ImGui.TextWrapped("The time for slidecasting is 50cs (half a second) by default.\n" +
                            "Lower numbers make it later in the cast, higher numbers earlier in the cast.\n" +
                            "Apart from missed packets, 50cs is the exact safe time to slidecast.");
                        ImGui.ColorEdit4("Bar Colour", ref slideCol, ImGuiColorEditFlags.NoInputs);
                        ImGui.Checkbox("Enable Debug Mode", ref debug);
                        ImGui.Separator();
                        /*
                        ImGui.ColorEdit4("Start", ref colp1_s);
                        ImGui.ColorEdit4("End", ref colp2_s);
                        if (ImGui.Button("Set"))
                        {
                            col1_s = new Colour(colp1_s.X, colp1_s.Y, colp1_s.Z, colp1_s.W);
                            col2_s = new Colour(colp2_s.X, colp2_s.Y, colp2_s.Z, colp2_s.W);

                            diffH_s = col2_s.Hue - col1_s.Hue;
                            diffS_s = col2_s.Saturation - col1_s.Saturation;
                            diffB_s = col2_s.Brightness - col1_s.Brightness;
                        }
                        */

                        if (ImGui.Button("Save and Close Config"))
                        {
                            SaveConfig();
                            config = false;
                        }
                        ImGui.End();
                    }

                    if (enabled)
                    {
                        if (castBar != tempCastBar)
                        {
                            castBar = IntPtr.Zero;
                        }

                        if (castBar != IntPtr.Zero)
                        {

                            cbCastLast = cbCastPer;
                            cbX = Marshal.ReadInt16(castBar + 0x1BC);
                            cbY = Marshal.ReadInt16(castBar + 0x1BE);
                            cbScale = Marshal.PtrToStructure<float>(castBar + 0x1AC);
                            cbCastTime = Marshal.ReadInt16(castBar + 0x2BC);
                            cbCastPer = Marshal.PtrToStructure<float>(castBar + 0x2C0);
                            int plus = 0;
                            cbSpell = new List<byte>();

                            while (Marshal.ReadByte(castBar + 0x242 + plus) != 0)
                            {
                                cbSpell.Add(Marshal.ReadByte(castBar + 0x242 + plus));
                                plus++;
                            }
                            cbSpellname = "";
                            foreach (byte bit in cbSpell)
                            {
                                cbSpellname += (char)bit;
                            }


                            if (cbCastLast == cbCastPer)
                            {
                                if (cbCastSameCount < 5)
                                { cbCastSameCount++; }

                            }
                            else
                            {
                                cbCastSameCount = 0;
                            }

                            if (cbCastPer == 5)
                            {
                                col_s = new Colour(col1_s.R / 255f, col1_s.G / 255f, col1_s.B / 255f);
                            }




                            if (Marshal.ReadByte(castBar + 0x182).ToString() != "84")
                            {

                                ImGui.Begin("SlideCast", ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoFocusOnAppearing);
                                ImGui.SetWindowPos(new Num.Vector2(cbX, cbY));
                                ImGui.SetWindowSize(new Num.Vector2(220 * cbScale, 60 * cbScale));
                                //float time = (float)cbCastTime - (0.01f * cbCastPer * (float)cbCastTime);
                                float slidePer = ((float)cbCastTime - (float)slideTime) / (float)cbCastTime;
                                ImGui.GetWindowDrawList().AddRectFilled(
                                    new Num.Vector2(ImGui.GetWindowPos().X + (48 * cbScale) + (152 * slidePer * cbScale), ImGui.GetWindowPos().Y + (20 * cbScale)),
                                    new Num.Vector2(ImGui.GetWindowPos().X + (48 * cbScale) + 5 + (152 * slidePer * cbScale), ImGui.GetWindowPos().Y + (29 * cbScale)),
                                    ImGui.GetColorU32(slideCol));
                                ImGui.End();
                            }

                            if (uiCastBar)
                            {
                                if (cbCastTime == 0)
                                {
                                    cbCastTime++;
                                }
                                if (cbCastSameCount < 5)
                                {
                                    float slidePer = 100 * ((float)cbCastTime - (float)slideTime) / (float)cbCastTime;
                                    double castTimeSec = ((double)cbCastTime / 100) - ((double)cbCastTime / 100) * (cbCastPer / 100);
                                    ImGui.Begin("uiCastBar_Self", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground);

                                    col_s.setHSB(col1_s.Hue + (int)(diffH_s * (cbCastPer / 100)), col1_s.Saturation + (int)(diffS_s * (cbCastPer / 100)), col1_s.Brightness + (int)(diffB_s * (cbCastPer / 100)));

                                    Num.Vector2 centre = DrawCircleProgress(40f, 10f, 100, cbCastPer, col_s);

                                    Num.Vector2 lineStart = new Num.Vector2(centre.X + (35f * (float)Math.Sin((Math.PI / 100) * slidePer)), centre.Y + (35f * (float)Math.Cos((Math.PI / 100) * slidePer)));
                                    Num.Vector2 lineEnd =   new Num.Vector2(centre.X + (45f * (float)Math.Sin((Math.PI / 100) * slidePer)), centre.Y + (45f * (float)Math.Cos((Math.PI / 100) * slidePer)));

                                    ImGui.GetWindowDrawList().AddLine(lineStart, lineEnd, ImGui.GetColorU32(slideCol), 5f);

                                    ImGui.GetWindowDrawList().AddCircleFilled(
                                            new Num.Vector2(centre.X, centre.Y),
                                            35f,
                                            ImGui.GetColorU32(new Num.Vector4(0.2f, 0.2f, 0.2f, 0.2f)),
                                            100);
                                    ImGui.GetWindowDrawList().AddCircle(
                                        new Num.Vector2(centre.X, centre.Y),
                                        45f,
                                        ImGui.GetColorU32(new Num.Vector4(0.4f, 0.4f, 0.4f, 0.4f)),
                                        100);
                                    ImGui.GetWindowDrawList().AddCircleFilled(
                                        new Num.Vector2(centre.X, centre.Y),
                                        35f,
                                        ImGui.GetColorU32(new Num.Vector4(0.4f, 0.4f, 0.4f, 0.4f)),
                                        100);

                                    ImGui.SetCursorPos(new Num.Vector2(
                                        centre.X - ImGui.GetWindowPos().X - (ImGui.CalcTextSize(cbSpellname).X) / 2,
                                        centre.Y - 15f - ImGui.GetWindowPos().Y));
                                    ShadowFont(cbSpellname);

                                    ImGui.SetCursorPos(new Num.Vector2(
                                        centre.X - ImGui.GetWindowPos().X - (ImGui.CalcTextSize(String.Format("{0:0.00}", castTimeSec)).X) / 2,
                                        centre.Y - ImGui.GetWindowPos().Y));
                                    ShadowFont(String.Format("{0:0.00}", castTimeSec));

                                    ImGui.SetCursorPos(new Num.Vector2(
                                        centre.X - ImGui.GetWindowPos().X - (ImGui.CalcTextSize(cbCastPer.ToString() + "%").X) / 2,
                                        centre.Y + 15f - ImGui.GetWindowPos().Y));
                                    ShadowFont(cbCastPer.ToString() + "%%");

                                    ImGui.End();
                                }

                                if (pluginInterface.ClientState.LocalPlayer != null)
                                {
                                    if (pluginInterface.ClientState.LocalPlayer.TargetActorID != 0)
                                    {
                                        Dalamud.Game.ClientState.Actors.ActorTable actorTable = pluginInterface.ClientState.Actors;



                                        for (var k = 0; k < this.pluginInterface.ClientState.Actors.Length; k++)
                                        {
                                            var actor = this.pluginInterface.ClientState.Actors[k];

                                            if (actor == null)
                                                continue;

                                            if (pluginInterface.ClientState.LocalPlayer.TargetActorID == actor.ActorId)
                                            {
                                                float targetSpellCast = Marshal.PtrToStructure<float>(actor.Address + 0x1C74);
                                                float targetSpellTime = Marshal.PtrToStructure<float>(actor.Address + 0x1C78);
                                                float targetSpellPer = 100 * (1 - ((targetSpellTime - targetSpellCast) / targetSpellTime));

                                                ImGui.Begin("uiCastBar_Target", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground);
                                                ImGui.Text(targetSpellCast.ToString());
                                                ImGui.Text(targetSpellTime.ToString());
                                                ImGui.Text(targetSpellPer.ToString());
                                                Num.Vector2 centreT = DrawCircleProgress(40f, 10f, 100, targetSpellPer, col_s);


                                                ImGui.GetWindowDrawList().AddCircleFilled(
                                                    new Num.Vector2(centreT.X, centreT.Y),
                                                    35f,
                                                    ImGui.GetColorU32(new Num.Vector4(0.2f, 0.2f, 0.2f, 0.2f)),
                                                    100);
                                                ImGui.GetWindowDrawList().AddCircle(
                                                    new Num.Vector2(centreT.X, centreT.Y),
                                                    45f,
                                                    ImGui.GetColorU32(new Num.Vector4(0.4f, 0.4f, 0.4f, 0.4f)),
                                                    100);
                                                ImGui.GetWindowDrawList().AddCircleFilled(
                                                    new Num.Vector2(centreT.X, centreT.Y),
                                                    35f,
                                                    ImGui.GetColorU32(new Num.Vector4(0.4f, 0.4f, 0.4f, 0.4f)),
                                                    100);
                                                if (Marshal.ReadInt16(actor.Address + 0x1C44) != 0)
                                                {
                                                    //var actionRow = actionSheet.GetRow(Marshal.ReadInt16(actor.Address + 0x1C44));
                                                    //ImGui.Text(actionRow.Name);
                                                    ImGui.Text("THINGY");
                                                }
                                                ImGui.End();
                                            }

                                        }
                                    }
                                }

                            }
                        }
                        else
                        {

                            castBar = tempCastBar;
                        }

                        if (debug)
                        {
                            ImGui.Begin("SlideCast DEBUG", ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar);
                            ImGui.SetWindowPos(new Num.Vector2(cbX, cbY));
                            ImGui.SetWindowSize(new Num.Vector2(220 * cbScale, 60 * cbScale));
                            //float time = (float)cbCastTime - (0.01f * cbCastPer * (float)cbCastTime);
                            float slidePer = ((float)cbCastTime - (float)slideTime) / (float)cbCastTime;
                            ImGui.GetWindowDrawList().AddRectFilled(
                                new Num.Vector2(ImGui.GetWindowPos().X + (48 * cbScale) + (152 * slidePer * cbScale), ImGui.GetWindowPos().Y + (20 * cbScale)),
                                new Num.Vector2(ImGui.GetWindowPos().X + (48 * cbScale) + 5 + (152 * slidePer * cbScale), ImGui.GetWindowPos().Y + (29 * cbScale)),
                                ImGui.GetColorU32(slideCol));
                            ImGui.End();

                            ImGui.Begin("Slidecast Debug Values");
                            ImGui.Text("cbX: " + cbX.ToString());
                            ImGui.Text("cbY: " + cbY.ToString());
                            ImGui.Text("cbS: " + cbScale.ToString());
                            ImGui.Text("cbCastTime: " + cbCastTime.ToString());
                            ImGui.Text("cbCastPer: " + cbCastPer.ToString());
                            ImGui.Text("Mem Addr: " + castBar.ToString("X"));
                            ImGui.Text("Spell: " + cbSpellname);
                            ImGui.Text(col_s.Hue.ToString());
                            ImGui.Text(col_s.Saturation.ToString());
                            ImGui.Text(col_s.Brightness.ToString());
                            ImGui.End();
                        }
                    }
                }
                else
                {
                    check = false;
                }
            }

            if (wait > 0) { wait--; }
            else { check = true; }

        }

        public void SaveConfig()
        {
            Configuration.Enabled = enabled;
            Configuration.SlideTime = slideTime;
            Configuration.SlideCol = slideCol;
            this.pluginInterface.SavePluginConfig(Configuration);
        }

        public Num.Vector2 DrawCircleProgress(float radius, float thickness, int num_segments, float percent, Colour colr)
        {

            Num.Vector2 pos = ImGui.GetCursorPos();
            pos.X += ImGui.GetWindowPos().X + radius + thickness;
            pos.Y += ImGui.GetWindowPos().Y + radius + thickness;
            ImGui.GetWindowDrawList().PathClear();
            float a_min = 0;
            float a_max = (float)Math.PI * 2.0f * ((float)num_segments - (100 - percent)) / (float)num_segments;

            ImGui.GetWindowDrawList().PathArcTo(pos, radius, a_min, a_max, num_segments);
            ImGui.GetWindowDrawList().PathStroke(ImGui.GetColorU32(colr.RGBA()), false, thickness);
            return pos;

        }

        public void ShadowFont(string input)
        {

            var cur_pos = ImGui.GetCursorPos();
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(new Num.Vector4(0f, 0f, 0f, 1f)));
            ImGui.SetCursorPos(new Num.Vector2(cur_pos.X - 1, cur_pos.Y - 1)); ImGui.Text(input);
            ImGui.SetCursorPos(new Num.Vector2(cur_pos.X - 1, cur_pos.Y + 1)); ImGui.Text(input);
            ImGui.SetCursorPos(new Num.Vector2(cur_pos.X + 1, cur_pos.Y + 1)); ImGui.Text(input);
            ImGui.SetCursorPos(new Num.Vector2(cur_pos.X + 1, cur_pos.Y - 1)); ImGui.Text(input);
            ImGui.PopStyleColor();
            ImGui.SetCursorPos(cur_pos);
            ImGui.Text(input);

        }
    }

    public class Config : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public bool Enabled { get; set; } = false;
        public int? SlideTime { get; set; } = 50;
        public Num.Vector4 SlideCol { get; set; } = new Num.Vector4(1.0f, 1.0f, 1.0f, 1.0f);
        public Colour colStart;
        public Colour colEnd;

    }

    public class Colour
    {
        public float R { get; set; }
        public float G { get; set; }
        public float B { get; set; }
        public float A { get; set; }

        public int Hue { get; set; }
        public int Saturation { get; set; }
        public int Brightness { get; set; }

        public Colour(float Rc, float Gc, float Bc)
        {
            R = Rc * 255f;
            G = Gc * 255f;
            B = Bc * 255f;
            A = 255f;

            Hue = (int)GetHue();
            Saturation = (int)GetSaturation();
            Brightness = (int)GetBrightness();
        }

        public Colour(float Rc, float Gc, float Bc, float Ac)
        {
            R = Rc * 255f;
            G = Gc * 255f;
            B = Bc * 255f;
            A = Ac * 255f;

            Hue = (int)GetHue();
            Saturation = (int)GetSaturation();
            Brightness = (int)GetBrightness();
        }

        public Num.Vector3 RGB()
        {
            return new Num.Vector3(R / 255f, G / 255f, B / 255f);
        }

        public Num.Vector4 RGBA()
        {
            return new Num.Vector4(R / 255f, G / 255f, B / 255f, A / 255f);
        }

        public Num.Vector3 HSB()
        {
            return new Num.Vector3(Hue, Saturation, Brightness);
        }

        public void setHSB(int setterH, int setterS, int setterB)
        {
            Hue = setterH;
            Saturation = setterS;
            Brightness = setterB;
            var temp = GetRGB();
            R = temp.X;
            G = temp.Y;
            B = temp.Z;
        }

        private float GetHue()
        {
            if (R == G && G == B)
                return 0;
            var r = R / 255f;
            var g = G / 255f;
            var b = B / 255f;
            float hue;
            var min = Numbers.Min(r, g, b);
            var max = Numbers.Max(r, g, b);
            var delta = max - min;
            if (r.AlmostEquals(max))
                hue = (g - b) / delta; // between yellow & magenta
            else if (g.AlmostEquals(max))
                hue = 2 + (b - r) / delta; // between cyan & yellow
            else
                hue = 4 + (r - g) / delta; // between magenta & cyan
            hue *= 60; // degrees
            if (hue < 0)
                hue += 360;
            return hue * 182.04f;
        }
        private float GetSaturation()
        {
            var r = R / 255f;
            var g = G / 255f;
            var b = B / 255f;
            var min = Numbers.Min(r, g, b);
            var max = Numbers.Max(r, g, b);
            if (max.AlmostEquals(min))
                return 0;
            return ((max.AlmostEquals(0f)) ? 0f : 1f - (1f * min / max)) * 255;
        }
        private float GetBrightness()
        {
            var r = R / 255f;
            var g = G / 255f;
            var b = B / 255f;
            return Numbers.Max(r, g, b) * 255;
        }

        public Num.Vector3 GetRGB()
        {
            var hue = (double)Hue;
            var saturation = (double)Saturation;
            var brightness = (double)Brightness;
            //Convert Hue into degrees for HSB
            hue = hue / 182.04;
            //Bri and Sat must be values from 0-1 (~percentage)
            brightness = brightness / 255.0;
            saturation = saturation / 255.0;
            double r = 0;
            double g = 0;
            double b = 0;
            if (saturation == 0)
            {
                r = g = b = brightness;
            }
            else
            {
                // the color wheel consists of 6 sectors.
                double sectorPos = hue / 60.0;
                int sectorNumber = (int)(Math.Floor(sectorPos));
                // get the fractional part of the sector
                double fractionalSector = sectorPos - sectorNumber;
                // calculate values for the three axes of the color.
                double p = brightness * (1.0 - saturation);
                double q = brightness * (1.0 - (saturation * fractionalSector));
                double t = brightness * (1.0 - (saturation * (1 - fractionalSector)));
                // assign the fractional colors to r, g, and b based on the sector the angle is in.
                switch (sectorNumber)
                {
                    case 0:
                        r = brightness;
                        g = t;
                        b = p;
                        break;
                    case 1:
                        r = q;
                        g = brightness;
                        b = p;
                        break;
                    case 2:
                        r = p;
                        g = brightness;
                        b = t;
                        break;
                    case 3:
                        r = p;
                        g = q;
                        b = brightness;
                        break;
                    case 4:
                        r = t;
                        g = p;
                        b = brightness;
                        break;
                    case 5:
                        r = brightness;
                        g = p;
                        b = q;
                        break;
                }
            }
            //Check if any value is out of byte range
            if (r < 0)
            {
                r = 0;
            }
            if (g < 0)
            {
                g = 0;
            }
            if (b < 0)
            {
                b = 0;
            }
            return new Num.Vector3((int)(r * 255.0), (int)(g * 255.0), (int)(b * 255.0));
        }
    }



    public static class FloatExtension
    {
        ///
        /// Tests equality with a certain amount of precision. Default to smallest possible double
        ///
        ///first value ///second value ///optional, smallest possible double value ///
        public static bool AlmostEquals(this float a, float b, double precision = float.Epsilon)
        {
            return Math.Abs(a - b) <= precision;
        }
    }
    public static class Numbers
    {
        public static float Max(params float[] numbers)
        {
            return numbers.Max();
        }
        public static float Min(params float[] numbers)
        {
            return numbers.Min();
        }
    }

}
