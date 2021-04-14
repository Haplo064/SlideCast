using System;
using Dalamud.Plugin;
using ImGuiNET;
using Dalamud.Interface;
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
        private DalamudPluginInterface _pI;
        private Config _configuration;
        private bool _enabled;
        private bool _config;
        private bool _debug;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetBaseUiObjDelegate();
        private GetBaseUiObjDelegate _getBaseUiObj;
        [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
        private delegate IntPtr GetUi2ObjByNameDelegate(IntPtr getBaseUiObj, string uiName, int index);
        private GetUi2ObjByNameDelegate _getUi2ObjByName;
        private IntPtr _scan1;
        private IntPtr _scan2;
        private IntPtr _castBar;
        private int _cbX;
        private int _cbY;
        private float _cbScale;
        private int _cbCastTime;
        private float _cbCastPer;
        private int _slideTime = 50;
        private Num.Vector4 _slideCol;
        private int _wait = 1000;
        private bool _check = true;
        private float _cbCastLast;
        private int _cbCastSameCount;
        private List<byte> _cbSpell = new List<byte>();
        private Colour _colS = new Colour(0.04f, 0.8f, 1f, 1f);
        private readonly Colour _col1S = new Colour(0.04f, 0.4f, 1f, 1f);

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            _pI = pluginInterface;
            _configuration = pluginInterface.GetPluginConfig() as Config ?? new Config();
            _scan1 = pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 41 b8 01 00 00 00 48 8d 15 ?? ?? ?? ?? 48 8b 48 20 e8 ?? ?? ?? ?? 48 8b cf");
            _scan2 = pluginInterface.TargetModuleScanner.ScanText("e8 ?? ?? ?? ?? 48 8b cf 48 89 87 ?? ?? 00 00 e8 ?? ?? ?? ?? 41 b8 01 00 00 00");
            _getBaseUiObj = Marshal.GetDelegateForFunctionPointer<GetBaseUiObjDelegate>(_scan1);
            _getUi2ObjByName = Marshal.GetDelegateForFunctionPointer<GetUi2ObjByNameDelegate>(_scan2);
            _castBar = _getUi2ObjByName(Marshal.ReadIntPtr(_getBaseUiObj(), 0x20), "_CastBar", 1) != IntPtr.Zero ? _getUi2ObjByName(Marshal.ReadIntPtr(_getBaseUiObj(), 0x20), "_CastBar", 1) : IntPtr.Zero;
            _pI.UiBuilder.OnBuildUi += DrawWindow;
            _pI.UiBuilder.OnOpenConfigUi += ConfigWindow;
            _pI.CommandManager.AddHandler("/slc", new CommandInfo(Command)
            {
                HelpMessage = "Open SlideCast config menu"
            });
            
             _enabled = _configuration.Enabled;
             _slideTime = _configuration.SlideTime;
             _slideCol = _configuration.SlideCol;

             pluginInterface.ClientState.OnLogout += (s, e) => _enabled = false;
             pluginInterface.ClientState.OnLogin += (s, e) => _enabled = _configuration.Enabled;
        }

        private void Command(string command, string arguments)
        {
            _config = true;
        }

        public void Dispose()
        {
            _pI.UiBuilder.OnBuildUi -= DrawWindow;
            _pI.UiBuilder.OnOpenConfigUi -= ConfigWindow;
            _pI.CommandManager.RemoveHandler("/slc");
        }

        private void ConfigWindow(object sender, EventArgs args)
        {
            _config = true;
        }

        private void DrawWindow()
        {
            if (_check)
            {
                var tempCastBar = _getUi2ObjByName(Marshal.ReadIntPtr(_getBaseUiObj(), 0x20), "_CastBar", 1);
                if (tempCastBar != IntPtr.Zero)
                {
                    _wait = 1000;
                    if (_config)
                    {
                        ImGui.SetNextWindowSize(new Num.Vector2(300, 500), ImGuiCond.FirstUseEver);
                        ImGui.Begin("SlideCast Config", ref _config);
                        ImGui.InputInt("Time (cs)", ref _slideTime);
                        ImGui.TextWrapped("The time for slidecasting is 50cs (half a second) by default.\n" +
                            "Lower numbers make it later in the cast, higher numbers earlier in the cast.\n" +
                            "Apart from missed packets, 50cs is the exact safe time to slidecast.");
                        ImGui.ColorEdit4("Bar Colour", ref _slideCol, ImGuiColorEditFlags.NoInputs);
                        ImGui.Checkbox("Enable Debug Mode", ref _debug);
                        ImGui.Separator();
                        if (ImGui.Button("Save and Close Config"))
                        {
                            SaveConfig();
                            _config = false;
                        }
                        ImGui.SameLine();
                        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
                        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);

                        if (ImGui.Button("Buy Haplo a Hot Chocolate"))
                        {
                            System.Diagnostics.Process.Start("https://ko-fi.com/haplo");
                        }
                        ImGui.PopStyleColor(3);
                        ImGui.End();
                    }

                    if (_enabled)
                    {
                        if (_castBar != tempCastBar)
                        {
                            _castBar = IntPtr.Zero;
                        }

                        if (_castBar != IntPtr.Zero)
                        {
                            _cbCastLast = _cbCastPer;
                            _cbX = Marshal.ReadInt16(_castBar + 0x1BC);
                            _cbY = Marshal.ReadInt16(_castBar + 0x1BE);
                            _cbScale = Marshal.PtrToStructure<float>(_castBar + 0x1AC);
                            _cbCastTime = Marshal.ReadInt16(_castBar + 0x2BC);
                            _cbCastPer = Marshal.PtrToStructure<float>(_castBar + 0x2C0);
                            var plus = 0;
                            _cbSpell = new List<byte>();

                            while (Marshal.ReadByte(_castBar + 0x242 + plus) != 0)
                            {
                                _cbSpell.Add(Marshal.ReadByte(_castBar + 0x242 + plus));
                                plus++;
                            }
                            
                            if (_cbCastLast == _cbCastPer)
                            {
                                if (_cbCastSameCount < 5)
                                { _cbCastSameCount++; }

                            }
                            else
                            {
                                _cbCastSameCount = 0;
                            }

                            if (_cbCastPer == 5)
                            {
                                _colS = new Colour(_col1S.R / 255f, _col1S.G / 255f, _col1S.B / 255f);
                            }

                            if (Marshal.ReadByte(_castBar + 0x182).ToString() != "84")
                            {
                                ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Num.Vector2(_cbX, _cbY));
                                ImGui.Begin("SlideCast", ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground);
                                ImGui.SetWindowSize(new Num.Vector2(220 * _cbScale, 60 * _cbScale));
                                //float time = (float)cbCastTime - (0.01f * cbCastPer * (float)cbCastTime);
                                float slidePer = ((float)_cbCastTime - (float)_slideTime) / (float)_cbCastTime;
                                ImGui.GetWindowDrawList().AddRectFilled(
                                    new Num.Vector2(
                                        ImGui.GetWindowPos().X + (48 * _cbScale) + (152 * slidePer * _cbScale),
                                        ImGui.GetWindowPos().Y + (20 * _cbScale)),
                                    new Num.Vector2(
                                        ImGui.GetWindowPos().X + (48 * _cbScale) + 5 + (152 * slidePer * _cbScale), 
                                        ImGui.GetWindowPos().Y + (29 * _cbScale)),
                                    ImGui.GetColorU32(_slideCol));
                                ImGui.End();
                            }
                        }
                        else
                        {
                            _castBar = tempCastBar;
                        }

                        if (_debug)
                        {
                            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Num.Vector2(_cbX, _cbY));
                            ImGui.Begin("SlideCast DEBUG", ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar);
                            ImGui.SetWindowSize(new Num.Vector2(220 * _cbScale, 60 * _cbScale));
                            //float time = (float)cbCastTime - (0.01f * cbCastPer * (float)cbCastTime);
                            var slidePer = ((float)_cbCastTime - (float)_slideTime) / (float)_cbCastTime;
                            ImGui.GetWindowDrawList().AddRectFilled(
                                new Num.Vector2(
                                    ImGui.GetWindowPos().X + (48 * _cbScale) + (152 * slidePer * _cbScale),
                                    ImGui.GetWindowPos().Y + (20 * _cbScale)),
                                new Num.Vector2(
                                    ImGui.GetWindowPos().X + (48 * _cbScale) + 5 + (152 * slidePer * _cbScale), 
                                    ImGui.GetWindowPos().Y + (29 * _cbScale)),
                                ImGui.GetColorU32(_slideCol));
                            ImGui.End();

                            ImGui.Begin("Slidecast Debug Values");
                            ImGui.Text("cbX: " + _cbX);
                            ImGui.Text("cbY: " + _cbY);
                            ImGui.Text("cbS: " + _cbScale);
                            ImGui.Text("cbCastTime: " + _cbCastTime);
                            ImGui.Text("cbCastPer: " + _cbCastPer);
                            ImGui.Text("Mem Addr: " + _castBar.ToString("X"));
                            ImGui.Text(_colS.Hue.ToString());
                            ImGui.Text(_colS.Saturation.ToString());
                            ImGui.Text(_colS.Brightness.ToString());
                            ImGui.End();
                        }
                    }
                }
                else
                {
                    _check = false;
                }
            }

            if (_wait > 0) { _wait--; }
            else { _check = true; }

        }

        private void SaveConfig()
        {
            _configuration.Enabled = _enabled;
            _configuration.SlideTime = _slideTime;
            _configuration.SlideCol = _slideCol;
            _pI.SavePluginConfig(_configuration);
        }
    }

    public class Config : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public bool Enabled { get; set; } = true;
        public int SlideTime { get; set; } = 50;
        public Num.Vector4 SlideCol { get; set; } = new Num.Vector4(1.0f, 1.0f, 1.0f, 1.0f);

    }

    public class Colour
    {
        public float R { get; set; }
        public float G { get; set; }
        public float B { get; set; }
        private float A { get; set; }

        public int Hue { get; set; }
        public int Saturation { get; set; }
        public int Brightness { get; set; }

        public Colour(float rc, float gc, float bc)
        {
            R = rc * 255f;
            G = gc * 255f;
            B = bc * 255f;
            A = 255f;

            Hue = (int)GetHue();
            Saturation = (int)GetSaturation();
            Brightness = (int)GetBrightness();
        }

        public Colour(float rc, float gc, float bc, float ac)
        {
            R = rc * 255f;
            G = gc * 255f;
            B = bc * 255f;
            A = ac * 255f;

            Hue = (int)GetHue();
            Saturation = (int)GetSaturation();
            Brightness = (int)GetBrightness();
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
    }



    public static class FloatExtension
    {
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
