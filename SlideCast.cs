using System;
using Dalamud.Plugin;
using ImGuiNET;
using Dalamud.Configuration;
using Num = System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.Command;


namespace Dev
{
    public class Dev : IDalamudPlugin
    {
        public string Name => "Slide Cast";
        private DalamudPluginInterface pluginInterface;
        public Config Configuration;

        public bool enabled = true;
        public bool config = false;

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


        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
            Configuration = pluginInterface.GetPluginConfig() as Config ?? new Config();


            scan1 = pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 41 b8 01 00 00 00 48 8d 15 ?? ?? ?? ?? 48 8b 48 20 e8 ?? ?? ?? ?? 48 8b cf");
            scan2 = pluginInterface.TargetModuleScanner.ScanText("e8 ?? ?? ?? ?? 48 8b cf 48 89 87 ?? ?? 00 00 e8 ?? ?? ?? ?? 41 b8 01 00 00 00");

            getBaseUIObj = Marshal.GetDelegateForFunctionPointer<GetBaseUIObjDelegate>(scan1);
            getUI2ObjByName = Marshal.GetDelegateForFunctionPointer<GetUI2ObjByNameDelegate>(scan2);
            castBar = getUI2ObjByName(Marshal.ReadIntPtr(getBaseUIObj(), 0x20), "ChatLog", 1);

            this.pluginInterface.UiBuilder.OnBuildUi += DrawWindow;
            this.pluginInterface.UiBuilder.OnOpenConfigUi += ConfigWindow;
            this.pluginInterface.CommandManager.AddHandler("/slc", new CommandInfo(Command)
            {
                HelpMessage = ""
            });

            try
            { enabled = Configuration.Enabled; }
            catch (Exception)
            {
                PluginLog.LogError("Failed to set Enabled");
                enabled = false;
            }

        }

        public void Command(string command, string arguments)
        {
            config = true;
        }

        public void Dispose()
        {
            this.pluginInterface.UiBuilder.OnBuildUi -= DrawWindow;
            this.pluginInterface.UiBuilder.OnOpenConfigUi -= ConfigWindow;
        }

        private void ConfigWindow(object Sender, EventArgs args)
        {
            config = true;
        }

        private void DrawWindow()
        {
            if (config)
            {
                ImGui.SetNextWindowSize(new Num.Vector2(300, 500), ImGuiCond.FirstUseEver);
                ImGui.Begin("SlideCast Config", ref config);
                ImGui.Checkbox("Enable", ref enabled);
                if (ImGui.Button("Save and Close Config"))
                {
                    SaveConfig();
                    config = false;
                }
                ImGui.End();
            }

            if (enabled)
            {
                if (castBar.ToString() != "0")
                {
                    cbX = Marshal.ReadInt16(castBar + 0x1BC);
                    cbY = Marshal.ReadInt16(castBar + 0x1BE);
                    cbScale = Marshal.PtrToStructure<float>(castBar + 0x1AC);
                    cbCastTime = Marshal.ReadInt16(castBar + 0x2BC);
                    cbCastPer = Marshal.PtrToStructure<float>(castBar + 0x2C0);

                    if (Marshal.ReadByte(castBar + 0x182).ToString() != "84")
                    {
                        ImGui.Begin("SlideCast", ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground);
                        ImGui.SetWindowPos(new Num.Vector2(cbX, cbY));
                        ImGui.SetWindowSize(new Num.Vector2(220 * cbScale, 60 * cbScale));
                        //float time = (float)cbCastTime - (0.01f * cbCastPer * (float)cbCastTime);
                        float slidePer = ((float)cbCastTime - 60f) / (float)cbCastTime;
                        ImGui.GetWindowDrawList().AddRectFilled(
                            new Num.Vector2(ImGui.GetWindowPos().X + (48 * cbScale) + (152 * slidePer * cbScale), ImGui.GetWindowPos().Y + (20 * cbScale)),
                            new Num.Vector2(ImGui.GetWindowPos().X + (48 * cbScale) + 5 + (152 * slidePer * cbScale), ImGui.GetWindowPos().Y + (29 * cbScale)),
                            UintCol(255, 255, 255, 255));
                        ImGui.End();
                    }
                }
                else
                {
                    castBar = getUI2ObjByName(Marshal.ReadIntPtr(getBaseUIObj(), 0x20), "_CastBar", 1);
                }
            }
        }

        public void SaveConfig()
        {
            Configuration.Enabled = enabled;
            this.pluginInterface.SavePluginConfig(Configuration);
        }

        public uint UintCol(int A, int B, int G, int R)
        {
            return Convert.ToUInt32("0x" + A.ToString("X2") + B.ToString("X2") + G.ToString("X2") + R.ToString("X2"), 16);
        }

    }

    public class Config : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public bool Enabled { get; set; } = false;
    }
}
